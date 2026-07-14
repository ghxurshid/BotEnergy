using System.Text.Json;
using Domain.Dtos.PaymentSession;
using Domain.Entities;
using Domain.Enums;
using Domain.Helpers;
using Domain.Interfaces;
using Domain.Interfaces.Payme;
using Domain.Options;
using Domain.Repositories;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Application.Services
{
    /// <summary>
    /// Hold invoice moliyaviy yakunlash yadrosi. FIFO consume, settlement maqsadlarini
    /// qo'yish, watcher polling + capture/refund ijrosi (retry/backoff), Settling sessiyani yopish.
    /// Payme'ga tegadigan barcha amallar SHU YERDA — SessionApi process'ida.
    /// </summary>
    public class HoldSettlementService : IHoldSettlementService
    {
        private readonly IHoldInvoiceRepository _invoiceRepo;
        private readonly IPaymentSessionRepository _paymentSessionRepo;
        private readonly ISessionRepository _sessionRepo;
        private readonly IDeviceRepository _deviceRepo;
        private readonly IProductProcessRepository _processRepo;
        private readonly IPaymeClient _payme;
        private readonly IPaymeCredentialResolver _credResolver;
        private readonly ISessionNotifier _notifier;
        private readonly IDeviceCommandPublisher _commandPublisher;
        private readonly IPushNotificationService _push;
        private readonly ITransactionRunner _tx;
        private readonly HoldInvoiceOptions _options;
        private readonly ILogger<HoldSettlementService> _logger;

        public HoldSettlementService(
            IHoldInvoiceRepository invoiceRepo,
            IPaymentSessionRepository paymentSessionRepo,
            ISessionRepository sessionRepo,
            IDeviceRepository deviceRepo,
            IProductProcessRepository processRepo,
            IPaymeClient payme,
            IPaymeCredentialResolver credResolver,
            ISessionNotifier notifier,
            IDeviceCommandPublisher commandPublisher,
            IPushNotificationService push,
            ITransactionRunner tx,
            IOptions<HoldInvoiceOptions> options,
            ILogger<HoldSettlementService> logger)
        {
            _invoiceRepo = invoiceRepo;
            _paymentSessionRepo = paymentSessionRepo;
            _sessionRepo = sessionRepo;
            _deviceRepo = deviceRepo;
            _processRepo = processRepo;
            _payme = payme;
            _credResolver = credResolver;
            _notifier = notifier;
            _commandPublisher = commandPublisher;
            _push = push;
            _tx = tx;
            _options = options.Value;
            _logger = logger;
        }

        // ── Funding: mavjud hold balans ─────────────────────────────
        public async Task<long> GetAvailableHoldTiyinAsync(long sessionId)
        {
            var ps = await _paymentSessionRepo.GetBySessionIdAsync(sessionId);
            if (ps is null || ps.Status != PaymentSessionStatus.Active)
                return 0;
            return Math.Max(0, ps.HoldBalanceTiyin - ps.ConsumedTiyin);
        }

        // ── Funding: dispense narxini hold'lardan FIFO consume ──────
        public async Task<decimal> ConsumeForProcessAsync(long processId)
        {
            return await _tx.RunAsync(async () =>
            {
                var process = await _processRepo.GetByIdWithSessionAsync(processId);
                if (process is null || process.Session is null)
                    return 0m;

                await _processRepo.ReloadAsync(process);

                // Yagona claim — device-finished / watchdog / session-close parallel chaqirsa
                // faqat bittasi yutadi (legacy bilan bir xil bayroq).
                if (!await _processRepo.TryClaimBalanceDeductionAsync(processId))
                    return 0m;

                var costTiyin = Money.ToTiyin(process.GivenAmount * process.PricePerUnit);
                if (costTiyin <= 0)
                    return 0m;

                var ps = await _paymentSessionRepo.GetBySessionIdAsync(process.Session.Id);
                if (ps is null)
                {
                    _logger.LogWarning("[HOLD-SETTLE] processId={ProcessId} uchun payment session topilmadi.", processId);
                    return 0m;
                }

                var invoices = await _invoiceRepo.GetByPaymentSessionAsync(ps.Id);
                long remaining = costTiyin;
                long totalApplied = 0;

                foreach (var invoice in invoices.OrderBy(i => i.SequenceNo))
                {
                    if (remaining <= 0) break;
                    if (invoice.Status is not (HoldInvoiceStatus.Hold or HoldInvoiceStatus.PartiallyConsumed))
                        continue;

                    var applied = await _invoiceRepo.ConsumeAtomicAsync(invoice.Id, remaining);
                    if (applied <= 0) continue;

                    await _paymentSessionRepo.TryConsumeBalanceAsync(ps.Id, applied);

                    // Consume'dan keyin invoice holatini yangilash (Hold→Partial/Fully).
                    var newConsumed = invoice.ConsumedTiyin + applied;
                    var target = newConsumed >= invoice.AmountTiyin
                        ? HoldInvoiceStatus.FullyConsumed
                        : HoldInvoiceStatus.PartiallyConsumed;
                    // PartiallyConsumed→PartiallyConsumed ruxsat jadvalida yo'q — o'sha holatda no-op.
                    if (invoice.Status != target)
                        await _invoiceRepo.TryTransitionAsync(invoice.Id, target);

                    await LogStepAsync(invoice, ps, HoldInvoiceStepType.ConsumeApplied, PaymentStepStatus.Info,
                        message: $"processId={processId} applied={applied} tiyin (seq={invoice.SequenceNo})");

                    remaining -= applied;
                    totalApplied += applied;
                }

                if (remaining > 0)
                    _logger.LogWarning(
                        "[HOLD-SETTLE] Hold yetmadi: processId={ProcessId} cost={Cost} applied={Applied} qoldi={Remaining} tiyin",
                        processId, costTiyin, totalApplied, remaining);

                await PublishBalanceAsync(ps, BalanceChangeReasons.Consumed, null);

                return Money.ToUzs(totalApplied);
            });
        }

        // ── Sessiya yopilishi: maqsad holatlarni qo'yish ────────────
        public async Task<bool> BeginSessionSettlementAsync(long sessionId, CancellationToken ct = default)
        {
            var ps = await _paymentSessionRepo.GetBySessionIdAsync(sessionId);
            if (ps is null)
                return false; // hold funding yo'q

            if (!await _invoiceRepo.AnyNonTerminalAsync(ps.Id))
            {
                // Hammasi terminal — settling shart emas, lekin payment session'ni yopamiz.
                await _paymentSessionRepo.TryTransitionAsync(ps.Id,
                    PaymentSessionStatus.Settled, PaymentSessionStatus.Active, PaymentSessionStatus.Settling);
                return false;
            }

            // Active → Settling (yangi invoice yaratishni bloklaydi).
            await _paymentSessionRepo.TryTransitionAsync(ps.Id, PaymentSessionStatus.Settling, PaymentSessionStatus.Active);

            var invoices = await _invoiceRepo.GetByPaymentSessionAsync(ps.Id);
            var now = DateTime.Now;

            foreach (var invoice in invoices)
            {
                switch (invoice.Status)
                {
                    case HoldInvoiceStatus.Hold when invoice.ConsumedTiyin == 0:
                        // Umuman ishlatilmadi — to'liq qaytariladi.
                        await _invoiceRepo.TryTransitionAsync(invoice.Id, HoldInvoiceStatus.RefundPending, nextAttemptAt: now);
                        await LogStepAsync(invoice, ps, HoldInvoiceStepType.SettlementTargetAssigned, PaymentStepStatus.Info,
                            message: "Ishlatilmagan Hold → RefundPending");
                        break;

                    case HoldInvoiceStatus.Hold:
                    case HoldInvoiceStatus.PartiallyConsumed:
                    case HoldInvoiceStatus.FullyConsumed:
                        // Ishlatilgan qism yechiladi (qisman capture), qolgani Payme'da avtomatik bo'shaydi.
                        await _invoiceRepo.TryTransitionAsync(invoice.Id, HoldInvoiceStatus.CapturePending,
                            captureAmountTiyin: invoice.ConsumedTiyin, nextAttemptAt: now);
                        await LogStepAsync(invoice, ps, HoldInvoiceStepType.SettlementTargetAssigned, PaymentStepStatus.Info,
                            message: $"Consumed={invoice.ConsumedTiyin} tiyin → CapturePending");
                        break;

                    case HoldInvoiceStatus.WaitingForConfirmation:
                        // To'lanmagan / poyga — watcher receipts.cancel qiladi (paid bo'lsa pul qaytadi).
                        await _invoiceRepo.TryTransitionAsync(invoice.Id, HoldInvoiceStatus.RefundPending, nextAttemptAt: now);
                        await LogStepAsync(invoice, ps, HoldInvoiceStepType.SettlementTargetAssigned, PaymentStepStatus.Info,
                            message: "WaitingForConfirmation → RefundPending (cancel)");
                        break;

                    case HoldInvoiceStatus.Created:
                        await _invoiceRepo.TryTransitionAsync(invoice.Id, HoldInvoiceStatus.Cancelled);
                        break;

                    case HoldInvoiceStatus.CapturePending:
                    case HoldInvoiceStatus.RefundPending:
                        // Allaqachon maqsad qo'yilgan (masalan user cancel) — darhol ishlov berilsin.
                        await _invoiceRepo.SchedulePollAsync(invoice.Id, now);
                        break;
                }
            }

            _logger.LogInformation("[HOLD-SETTLE] Sessiya settlement boshlandi sessionId={SessionId} paymentSessionId={PsId}",
                sessionId, ps.Id);
            return true;
        }

        // ── Watcher tick: navbatdagi invoice'lar ────────────────────
        public async Task ProcessDueInvoicesAsync(string ownerId, CancellationToken ct = default)
        {
            var leaseUntil = DateTime.Now.AddSeconds(_options.LeaseSeconds);
            var due = await _invoiceRepo.ClaimDueAsync(ownerId, leaseUntil, _options.BatchSize);

            foreach (var invoice in due)
            {
                if (ct.IsCancellationRequested) break;
                try
                {
                    await ProcessOneAsync(invoice, ct);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "[HOLD-WATCH] invoiceId={InvoiceId} ishlovida xato", invoice.Id);
                    await _invoiceRepo.ReleaseLeaseAsync(invoice.Id, ownerId);
                }
            }
        }

        private async Task ProcessOneAsync(HoldInvoiceEntity invoice, CancellationToken ct)
        {
            var ps = await _paymentSessionRepo.GetByIdAsync(invoice.PaymentSessionId);
            if (ps is null) return;

            var creds = await _credResolver.ForMerchantAsync(ps.MerchantId);
            if (creds is null)
            {
                await _invoiceRepo.ScheduleRetryAsync(invoice.Id, NextBackoff(invoice.AttemptCount),
                    "Merchant Payme credential yo'q.");
                return;
            }

            if (string.IsNullOrEmpty(invoice.ProviderReceiptId))
            {
                await _invoiceRepo.TryTransitionAsync(invoice.Id, HoldInvoiceStatus.Failed,
                    failureReason: "ProviderReceiptId yo'q.");
                return;
            }

            switch (invoice.Status)
            {
                case HoldInvoiceStatus.WaitingForConfirmation:
                    await PollWaitingAsync(invoice, ps, creds, ct);
                    break;
                case HoldInvoiceStatus.CapturePending:
                    await CaptureAsync(invoice, ps, creds, ct);
                    break;
                case HoldInvoiceStatus.RefundPending:
                    await RefundAsync(invoice, ps, creds, ct);
                    break;
                default:
                    await _invoiceRepo.SchedulePollAsync(invoice.Id, DateTime.Now.AddSeconds(_options.PollSeconds));
                    break;
            }
        }

        private async Task PollWaitingAsync(HoldInvoiceEntity invoice, PaymentSessionEntity ps, PaymeCredentials creds, CancellationToken ct)
        {
            var call = await _payme.CheckReceiptAsync(invoice.ProviderReceiptId!, creds, ct);
            await LogStepAsync(invoice, ps, HoldInvoiceStepType.CheckPolled,
                call.IsSuccess ? PaymentStepStatus.Info : PaymentStepStatus.Error,
                requestPayload: call.RequestBody, responsePayload: call.ResponseBody, message: call.FailureMessage);

            if (!call.IsSuccess)
            {
                await _invoiceRepo.SchedulePollAsync(invoice.Id, DateTime.Now.AddSeconds(_options.PollSeconds));
                return;
            }

            var state = call.Result!.State;

            if (state == PaymeReceiptStates.Held || state == PaymeReceiptStates.Paid)
            {
                if (await _invoiceRepo.TryTransitionAsync(invoice.Id, HoldInvoiceStatus.Hold))
                {
                    await _paymentSessionRepo.TryAddHoldBalanceAsync(ps.Id, invoice.AmountTiyin);
                    await LogStepAsync(invoice, ps, HoldInvoiceStepType.Held, PaymentStepStatus.Success,
                        message: $"state={state}, +{invoice.AmountTiyin} tiyin balansga");
                    await PublishBalanceAsync(ps, BalanceChangeReasons.InvoiceHeld, invoice.Id);
                }
                return;
            }

            if (state == PaymeReceiptStates.Cancelled)
            {
                await _invoiceRepo.TryTransitionAsync(invoice.Id, HoldInvoiceStatus.Cancelled);
                await LogStepAsync(invoice, ps, HoldInvoiceStepType.Cancelled, PaymentStepStatus.Info,
                    message: "Payme tomonda bekor qilingan.");
                return;
            }

            // Hali kutilmoqda — TTL tekshiruvi.
            var age = DateTime.Now - invoice.CreatedDate;
            if (age > TimeSpan.FromMinutes(_options.InvoiceTtlMinutes))
            {
                // To'lanmagan receiptni bekor qilib Expired qilamiz.
                var cancelCall = await _payme.CancelReceiptAsync(invoice.ProviderReceiptId!, creds, ct);
                await LogStepAsync(invoice, ps, HoldInvoiceStepType.Expired,
                    cancelCall.IsSuccess ? PaymentStepStatus.Info : PaymentStepStatus.Error,
                    requestPayload: cancelCall.RequestBody, responsePayload: cancelCall.ResponseBody,
                    message: $"TTL ({_options.InvoiceTtlMinutes}min) tugadi — bekor qilindi.");
                await _invoiceRepo.TryTransitionAsync(invoice.Id, HoldInvoiceStatus.Expired);
                return;
            }

            await _invoiceRepo.SchedulePollAsync(invoice.Id, DateTime.Now.AddSeconds(_options.PollSeconds), state);
        }

        private async Task CaptureAsync(HoldInvoiceEntity invoice, PaymentSessionEntity ps, PaymeCredentials creds, CancellationToken ct)
        {
            var captureTiyin = invoice.CaptureAmountTiyin ?? invoice.ConsumedTiyin;

            // Ishlatilmagan bo'lsa capture o'rniga to'liq refund.
            if (captureTiyin <= 0)
            {
                await _invoiceRepo.TryTransitionAsync(invoice.Id, HoldInvoiceStatus.RefundPending, nextAttemptAt: DateTime.Now);
                return;
            }

            await LogStepAsync(invoice, ps, HoldInvoiceStepType.CaptureRequested, PaymentStepStatus.Info,
                message: $"confirm_hold amount={captureTiyin} tiyin");

            var call = await _payme.ConfirmHoldAsync(invoice.ProviderReceiptId!, captureTiyin, creds, ct);
            await LogStepAsync(invoice, ps, HoldInvoiceStepType.CaptureResponded,
                call.IsSuccess ? PaymentStepStatus.Success : PaymentStepStatus.Error,
                requestPayload: call.RequestBody, responsePayload: call.ResponseBody, message: call.FailureMessage);

            var outcome = PaymeErrorClassifier.Classify(call);

            // "Holat mos emas" — chek allaqachon yechilganmi tekshiramiz.
            if (outcome == PaymeCallOutcome.AlreadyDone)
                outcome = await ReconcileAsync(invoice, creds, PaymeReceiptStates.Paid, ct);

            switch (outcome)
            {
                case PaymeCallOutcome.Success:
                case PaymeCallOutcome.AlreadyDone:
                    await _invoiceRepo.TryTransitionAsync(invoice.Id, HoldInvoiceStatus.Captured);
                    await PublishBalanceAsync(ps, BalanceChangeReasons.Captured, invoice.Id);
                    break;
                case PaymeCallOutcome.Transient:
                    await BackoffOrFailAsync(invoice, $"Capture: {call.FailureMessage}");
                    break;
                default:
                    await _invoiceRepo.TryTransitionAsync(invoice.Id, HoldInvoiceStatus.Failed,
                        failureReason: $"Capture (permanent): {call.FailureMessage}");
                    _logger.LogError("[HOLD-WATCH] Capture Failed invoiceId={InvoiceId}: {Msg}", invoice.Id, call.FailureMessage);
                    break;
            }
        }

        private async Task RefundAsync(HoldInvoiceEntity invoice, PaymentSessionEntity ps, PaymeCredentials creds, CancellationToken ct)
        {
            await LogStepAsync(invoice, ps, HoldInvoiceStepType.RefundRequested, PaymentStepStatus.Info,
                message: "receipts.cancel");

            var call = await _payme.CancelReceiptAsync(invoice.ProviderReceiptId!, creds, ct);
            await LogStepAsync(invoice, ps, HoldInvoiceStepType.RefundResponded,
                call.IsSuccess ? PaymentStepStatus.Success : PaymentStepStatus.Error,
                requestPayload: call.RequestBody, responsePayload: call.ResponseBody, message: call.FailureMessage);

            var outcome = PaymeErrorClassifier.Classify(call);
            if (outcome == PaymeCallOutcome.AlreadyDone)
                outcome = await ReconcileAsync(invoice, creds, PaymeReceiptStates.Cancelled, ct);

            switch (outcome)
            {
                case PaymeCallOutcome.Success:
                case PaymeCallOutcome.AlreadyDone:
                    await _invoiceRepo.TryTransitionAsync(invoice.Id, HoldInvoiceStatus.Refunded);
                    await PublishBalanceAsync(ps, BalanceChangeReasons.Refunded, invoice.Id);
                    break;
                case PaymeCallOutcome.Transient:
                    await BackoffOrFailAsync(invoice, $"Refund: {call.FailureMessage}");
                    break;
                default:
                    await _invoiceRepo.TryTransitionAsync(invoice.Id, HoldInvoiceStatus.Failed,
                        failureReason: $"Refund (permanent): {call.FailureMessage}");
                    _logger.LogError("[HOLD-WATCH] Refund Failed invoiceId={InvoiceId}: {Msg}", invoice.Id, call.FailureMessage);
                    break;
            }
        }

        /// <summary>
        /// StateMismatch kelганda receipts.check bilan haqiqiy holatni aniqlaydi:
        /// kutilgan terminal holatda bo'lsa AlreadyDone (idempotent success), aks holda Transient.
        /// </summary>
        private async Task<PaymeCallOutcome> ReconcileAsync(HoldInvoiceEntity invoice, PaymeCredentials creds, int expectedState, CancellationToken ct)
        {
            var check = await _payme.CheckReceiptAsync(invoice.ProviderReceiptId!, creds, ct);
            if (check.IsSuccess && check.Result!.State == expectedState)
                return PaymeCallOutcome.AlreadyDone;
            return PaymeCallOutcome.Transient;
        }

        private async Task BackoffOrFailAsync(HoldInvoiceEntity invoice, string reason)
        {
            if (invoice.AttemptCount + 1 >= _options.MaxAttempts)
            {
                await _invoiceRepo.TryTransitionAsync(invoice.Id, HoldInvoiceStatus.Failed,
                    failureReason: $"Retry limiti tugadi: {reason}");
                _logger.LogError("[HOLD-WATCH] invoiceId={InvoiceId} retry limiti tugadi — Failed", invoice.Id);
            }
            else
            {
                await _invoiceRepo.ScheduleRetryAsync(invoice.Id, NextBackoff(invoice.AttemptCount), reason);
            }
        }

        private DateTime NextBackoff(int attemptCount)
        {
            var seconds = Math.Min(
                _options.BackoffBaseSeconds * Math.Pow(2, attemptCount),
                _options.BackoffMaxSeconds);
            return DateTime.Now.AddSeconds(seconds);
        }

        // ── Watcher tick: Settling → Closed ─────────────────────────
        public async Task FinalizeSettledSessionsAsync(CancellationToken ct = default)
        {
            var settling = await _paymentSessionRepo.GetSettlingAsync(_options.BatchSize);

            foreach (var ps in settling)
            {
                if (ct.IsCancellationRequested) break;
                if (await _invoiceRepo.AnyNonTerminalAsync(ps.Id))
                    continue; // hali invoice'lar terminal emas

                if (!await _paymentSessionRepo.TryTransitionAsync(ps.Id,
                        PaymentSessionStatus.Settled, PaymentSessionStatus.Settling))
                    continue;

                await CloseParentSessionAsync(ps);
            }
        }

        private async Task CloseParentSessionAsync(PaymentSessionEntity ps)
        {
            var session = await _sessionRepo.GetByIdAsync(ps.SessionId);
            if (session is null || session.Status == SessionStatus.Closed)
                return;

            session.Status = SessionStatus.Closed;
            session.CloseReason ??= SessionCloseReason.UserClosed;
            session.ClosedAt = DateTime.Now;
            session.LastActivityAt = DateTime.Now;
            await _sessionRepo.UpdateAsync(session);

            if (session.Device is not null)
                await _commandPublisher.PublishSessionClosedAsync(
                    session.Device.SerialNumber, session.Id, session.CloseReason.ToString()!);

            await _notifier.NotifySessionClosedAsync(session.SessionToken, new
            {
                session_id = session.Id,
                reason = session.CloseReason.ToString(),
                settled = true,
                closed_at = session.ClosedAt
            });

            await _push.SendAsync(session.UserId, new PushNotification
            {
                Title = "Sessiya yakunlandi",
                Body = "To'lov hisob-kitobi yakunlandi. Ishlatilmagan mablag' qaytarildi.",
                DeepLink = $"botenergy://sessions/{session.Id}"
            });

            _logger.LogInformation("[HOLD-SETTLE] Settling sessiya yopildi sessionId={SessionId}", session.Id);
        }

        // ── Yagona balans event: SignalR + MQTT ─────────────────────
        private async Task PublishBalanceAsync(PaymentSessionEntity ps, string reason, long? invoiceId)
        {
            // Yangi qiymatlarni o'qiymiz (atomik update'lardan keyin).
            var fresh = await _paymentSessionRepo.GetByIdAsync(ps.Id) ?? ps;
            var available = Math.Max(0, fresh.HoldBalanceTiyin - fresh.ConsumedTiyin);

            var dto = new SessionBalanceChangedDto
            {
                SessionId = fresh.SessionId,
                PaymentSessionId = fresh.Id,
                InvoiceId = invoiceId,
                HoldBalanceTiyin = fresh.HoldBalanceTiyin,
                ConsumedTiyin = fresh.ConsumedTiyin,
                AvailableTiyin = available,
                AvailableUzs = Money.ToUzs(available),
                Reason = reason,
                CorrelationId = fresh.CorrelationId
            };

            var session = await _sessionRepo.GetByIdAsync(fresh.SessionId);
            if (session is not null)
                await _notifier.NotifySessionBalanceChangedAsync(session.SessionToken, fresh.UserId, dto);

            var device = await _deviceRepo.GetByIdAsync(fresh.DeviceId);
            if (device is not null)
            {
                try { await _commandPublisher.PublishBalanceUpdateAsync(device.SerialNumber, dto); }
                catch (Exception ex) { _logger.LogWarning(ex, "[HOLD-SETTLE] MQTT balance.update yuborilmadi serial={Serial}", device.SerialNumber); }
            }
        }

        private Task LogStepAsync(
            HoldInvoiceEntity invoice, PaymentSessionEntity ps,
            HoldInvoiceStepType stepType, PaymentStepStatus status,
            string? requestPayload = null, string? responsePayload = null, string? message = null)
            => _invoiceRepo.AddStepAsync(new HoldInvoiceStepEntity
            {
                HoldInvoiceId = invoice.Id,
                PaymentSessionId = ps.Id,
                SessionId = ps.SessionId,
                MerchantId = ps.MerchantId,
                DeviceId = ps.DeviceId,
                UserId = ps.UserId,
                StepType = stepType,
                Status = status,
                RequestPayload = EnsureJson(requestPayload),
                ResponsePayload = EnsureJson(responsePayload),
                Message = message,
                CorrelationId = ps.CorrelationId
            });

        private static string? EnsureJson(string? payload)
        {
            if (string.IsNullOrWhiteSpace(payload)) return null;
            try { using var _ = JsonDocument.Parse(payload); return payload; }
            catch (JsonException) { return JsonSerializer.Serialize(payload); }
        }
    }
}
