using System.Text.Json;
using Domain.Dtos.Base;
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
    /// Hold invoice yaratish/bekor qilish. receipts.create sync bajariladi (mobil receipt id kutadi),
    /// qolgan barcha Payme amallari (check/confirm_hold/cancel) — watcher'da.
    /// Har bir provider chaqiruvi hold_invoice_steps'ga audit sifatida yoziladi.
    /// </summary>
    public class HoldInvoiceService : IHoldInvoiceService
    {
        private readonly IHoldInvoiceRepository _invoiceRepo;
        private readonly IPaymentSessionRepository _paymentSessionRepo;
        private readonly ISessionRepository _sessionRepo;
        private readonly IDeviceRepository _deviceRepo;
        private readonly IPaymentSessionService _paymentSessionService;
        private readonly IPaymeClient _payme;
        private readonly IPaymeCredentialResolver _credResolver;
        private readonly IHoldSettlementService _holdSettlement;
        private readonly HoldInvoiceOptions _options;
        private readonly ILogger<HoldInvoiceService> _logger;

        public HoldInvoiceService(
            IHoldInvoiceRepository invoiceRepo,
            IPaymentSessionRepository paymentSessionRepo,
            ISessionRepository sessionRepo,
            IDeviceRepository deviceRepo,
            IPaymentSessionService paymentSessionService,
            IPaymeClient payme,
            IPaymeCredentialResolver credResolver,
            IHoldSettlementService holdSettlement,
            IOptions<HoldInvoiceOptions> options,
            ILogger<HoldInvoiceService> logger)
        {
            _invoiceRepo = invoiceRepo;
            _paymentSessionRepo = paymentSessionRepo;
            _sessionRepo = sessionRepo;
            _deviceRepo = deviceRepo;
            _paymentSessionService = paymentSessionService;
            _payme = payme;
            _credResolver = credResolver;
            _holdSettlement = holdSettlement;
            _options = options.Value;
            _logger = logger;
        }

        public async Task<GenericDto<HoldInvoiceResultDto>> CreateAsync(CreateHoldInvoiceDto dto, CancellationToken ct = default)
        {
            // ── 1. Validatsiya ──
            if (dto.AmountUzs <= 0)
                return GenericDto<HoldInvoiceResultDto>.Error(400, "Summa musbat bo'lishi kerak.");

            var session = await _sessionRepo.GetByIdAsync(dto.SessionId);
            if (session is null)
                return GenericDto<HoldInvoiceResultDto>.Error(404, "Sessiya topilmadi.");
            if (session.UserId != dto.UserId)
                return GenericDto<HoldInvoiceResultDto>.Error(403, "Bu sessiya sizga tegishli emas.");

            if (session.Status is not (SessionStatus.Connected or SessionStatus.InProcess))
                return GenericDto<HoldInvoiceResultDto>.Error(409,
                    session.Status switch
                    {
                        SessionStatus.Paused => "Sessiya pauzada (qurilma bilan aloqa yo'q) — yangi invoice yaratib bo'lmaydi.",
                        SessionStatus.Settling => "Sessiya hisob-kitob qilinmoqda — yangi invoice yaratib bo'lmaydi.",
                        _ => "Sessiya holati invoice yaratishga ruxsat bermaydi."
                    });

            // ── 2. Idempotency replay ──
            if (!string.IsNullOrEmpty(dto.IdempotencyKey))
            {
                var replay = await _invoiceRepo.GetByIdempotencyKeyAsync(dto.IdempotencyKey);
                if (replay is not null)
                    return GenericDto<HoldInvoiceResultDto>.Success(MapResult(replay, "Takroriy so'rov — mavjud invoice qaytarildi."));
            }

            // ── 3. PaymentSession (connect'da yaratilgan; yo'q bo'lsa lazy-create) ──
            var paymentSession = await _paymentSessionRepo.GetBySessionIdAsync(dto.SessionId);
            if (paymentSession is null)
            {
                if (session.DeviceId is null)
                    return GenericDto<HoldInvoiceResultDto>.Error(409, "Sessiyaga qurilma ulanmagan.");

                var device = await _deviceRepo.GetByIdAsync(session.DeviceId.Value);
                if (device?.Station is null)
                    return GenericDto<HoldInvoiceResultDto>.Error(409, "Qurilma stansiyaga biriktirilmagan.");

                paymentSession = await _paymentSessionService.CreateForSessionAsync(
                    dto.SessionId, device.Id, dto.UserId, device.Station.MerchantId);
            }

            if (paymentSession.Status != PaymentSessionStatus.Active)
                return GenericDto<HoldInvoiceResultDto>.Error(409, "To'lov konteksti aktiv emas (hisob-kitob boshlangan).");

            // ── 4. Limit ──
            var activeCount = await _invoiceRepo.CountActiveForPaymentSessionAsync(paymentSession.Id);
            if (activeCount >= _options.MaxInvoicesPerSession)
                return GenericDto<HoldInvoiceResultDto>.Error(400,
                    $"Bir sessiyada ko'pi bilan {_options.MaxInvoicesPerSession} ta aktiv invoice bo'lishi mumkin.");

            // ── 5. Merchant credential'lari (fallback YO'Q — sozlanmagan bo'lsa rad) ──
            var creds = await _credResolver.ForMerchantAsync(paymentSession.MerchantId);
            if (creds is null)
                return GenericDto<HoldInvoiceResultDto>.Error(409,
                    "Merchant uchun Payme sozlanmagan — administratorga murojaat qiling.");

            // ── 6. Invoice yozuvi (Created) ──
            var amountTiyin = Money.ToTiyin(dto.AmountUzs);
            var invoice = new HoldInvoiceEntity
            {
                PaymentSessionId = paymentSession.Id,
                SequenceNo = await _invoiceRepo.NextSequenceNoAsync(paymentSession.Id),
                AmountTiyin = amountTiyin,
                ProviderOrderId = $"hold-{paymentSession.Id}-{Guid.NewGuid():N}",
                IdempotencyKey = dto.IdempotencyKey,
                CreatedByUserId = dto.UserId
            };
            invoice = await _invoiceRepo.CreateAsync(invoice);

            await LogStepAsync(invoice, paymentSession, HoldInvoiceStepType.Initiated, PaymentStepStatus.Info,
                message: $"amount={amountTiyin} tiyin ({dto.AmountUzs} UZS), seq={invoice.SequenceNo}");
            await LogStepAsync(invoice, paymentSession, HoldInvoiceStepType.Validated, PaymentStepStatus.Success);

            // ── 7. Payme receipt (hold:true) — sync ──
            await LogStepAsync(invoice, paymentSession, HoldInvoiceStepType.ReceiptCreateRequested, PaymentStepStatus.Info,
                message: $"order_id={invoice.ProviderOrderId}");

            var createCall = await _payme.CreateReceiptAsync(
                amountTiyin, invoice.ProviderOrderId, hold: true,
                description: $"BotEnergy sessiya #{dto.SessionId} hold", creds: creds, ct: ct);

            await LogStepAsync(invoice, paymentSession, HoldInvoiceStepType.ReceiptCreated,
                createCall.IsSuccess ? PaymentStepStatus.Success : PaymentStepStatus.Error,
                requestPayload: createCall.RequestBody,
                responsePayload: createCall.ResponseBody,
                message: createCall.FailureMessage);

            if (!createCall.IsSuccess)
            {
                await _invoiceRepo.TryTransitionAsync(invoice.Id, HoldInvoiceStatus.Failed,
                    failureReason: $"Receipt yaratish: {createCall.FailureMessage}");
                return GenericDto<HoldInvoiceResultDto>.Error(502, "Payme bilan bog'lanishda xatolik.");
            }

            invoice.ProviderReceiptId = createCall.Result!.Id;
            invoice.ProviderState = createCall.Result.State;
            await _invoiceRepo.UpdateAsync(invoice);

            // ── 8. WaitingForConfirmation — watcher polling boshlaydi ──
            await _invoiceRepo.TryTransitionAsync(invoice.Id, HoldInvoiceStatus.WaitingForConfirmation,
                nextAttemptAt: DateTime.Now);
            invoice.Status = HoldInvoiceStatus.WaitingForConfirmation;

            // ── 9. Ixtiyoriy: SMS invoice ──
            if (_options.SendReceiptToPhone && !string.IsNullOrWhiteSpace(dto.Phone))
            {
                var sendCall = await _payme.SendReceiptAsync(invoice.ProviderReceiptId, dto.Phone, creds, ct);
                await LogStepAsync(invoice, paymentSession, HoldInvoiceStepType.SendRequested,
                    sendCall.IsSuccess ? PaymentStepStatus.Success : PaymentStepStatus.Error,
                    requestPayload: sendCall.RequestBody,
                    responsePayload: sendCall.ResponseBody,
                    message: sendCall.FailureMessage);
                // Send xatosi kritik emas — mijoz app ichida ham to'lashi mumkin.
            }

            _logger.LogInformation(
                "[HOLD-INV] Yaratildi invoiceId={InvoiceId} seq={Seq} amount={Amount} tiyin receipt={ReceiptId}",
                invoice.Id, invoice.SequenceNo, amountTiyin, invoice.ProviderReceiptId);

            // Sessiyadagi boshqa qurilmalar (planshet/telefon) yangi invoice'ni real-time ko'rsin.
            await _holdSettlement.PublishSessionHoldStateAsync(dto.SessionId, BalanceChangeReasons.InvoiceCreated, invoice.Id);

            return GenericDto<HoldInvoiceResultDto>.Success(MapResult(invoice,
                "Invoice yaratildi — Payme ilovasida to'lovni tasdiqlang."));
        }

        public async Task<GenericDto<HoldInvoiceResultDto>> CancelByUserAsync(long invoiceId, long userId, CancellationToken ct = default)
        {
            var invoice = await _invoiceRepo.GetByIdAsync(invoiceId);
            if (invoice is null)
                return GenericDto<HoldInvoiceResultDto>.Error(404, "Invoice topilmadi.");

            var paymentSession = await _paymentSessionRepo.GetByIdAsync(invoice.PaymentSessionId);
            if (paymentSession is null || paymentSession.UserId != userId)
                return GenericDto<HoldInvoiceResultDto>.Error(403, "Bu invoice sizga tegishli emas.");

            if (invoice.ConsumedTiyin > 0)
                return GenericDto<HoldInvoiceResultDto>.Error(409,
                    "Invoice mablag'i qisman ishlatilgan — bekor qilib bo'lmaydi, sessiyani yakunlang.");

            switch (invoice.Status)
            {
                case HoldInvoiceStatus.Hold:
                    // Pul ushlab turilibdi — refund maqsadi, watcher qaytaradi.
                    if (!await _invoiceRepo.TryTransitionAsync(invoiceId, HoldInvoiceStatus.RefundPending,
                            nextAttemptAt: DateTime.Now))
                        return GenericDto<HoldInvoiceResultDto>.Error(409, "Invoice holati o'zgargan — qayta urinib ko'ring.");

                    // Sessiya balansidan chiqarib qo'yamiz (bu mablag' endi ishlatilmaydi).
                    await _paymentSessionRepo.TryAddHoldBalanceAsync(paymentSession.Id, -invoice.AmountTiyin);

                    await LogStepAsync(invoice, paymentSession, HoldInvoiceStepType.SettlementTargetAssigned,
                        PaymentStepStatus.Info, message: "User cancel: Hold → RefundPending");
                    break;

                case HoldInvoiceStatus.Created:
                case HoldInvoiceStatus.WaitingForConfirmation:
                    // Hali to'lanmagan — receipt'ni darhol bekor qilamiz.
                    if (!string.IsNullOrEmpty(invoice.ProviderReceiptId))
                    {
                        var creds = await _credResolver.ForMerchantAsync(paymentSession.MerchantId);
                        var cancelCall = await _payme.CancelReceiptAsync(invoice.ProviderReceiptId, creds, ct);
                        await LogStepAsync(invoice, paymentSession, HoldInvoiceStepType.Cancelled,
                            cancelCall.IsSuccess ? PaymentStepStatus.Success : PaymentStepStatus.Error,
                            requestPayload: cancelCall.RequestBody,
                            responsePayload: cancelCall.ResponseBody,
                            message: cancelCall.FailureMessage);
                        // Cancel xatosi bo'lsa ham davom etamiz — to'lanmagan receipt TTL bilan o'zi o'ladi.
                    }

                    if (!await _invoiceRepo.TryTransitionAsync(invoiceId, HoldInvoiceStatus.Cancelled))
                        return GenericDto<HoldInvoiceResultDto>.Error(409, "Invoice holati o'zgargan — qayta urinib ko'ring.");
                    break;

                default:
                    return GenericDto<HoldInvoiceResultDto>.Error(409,
                        $"Invoice holati ({invoice.Status}) bekor qilishga ruxsat bermaydi.");
            }

            // Status o'zgardi (Cancelled yoki RefundPending) — sessiya guruhiga real-time yuboramiz.
            await _holdSettlement.PublishSessionHoldStateAsync(
                paymentSession.SessionId, BalanceChangeReasons.Cancelled, invoiceId);

            invoice = await _invoiceRepo.GetByIdAsync(invoiceId);
            return GenericDto<HoldInvoiceResultDto>.Success(MapResult(invoice!, "Invoice bekor qilindi."));
        }

        public async Task<GenericDto<List<HoldInvoiceItemDto>>> GetForSessionAsync(long sessionId, long userId)
        {
            var session = await _sessionRepo.GetByIdAsync(sessionId);
            if (session is null)
                return GenericDto<List<HoldInvoiceItemDto>>.Error(404, "Sessiya topilmadi.");
            if (session.UserId != userId)
                return GenericDto<List<HoldInvoiceItemDto>>.Error(403, "Bu sessiya sizga tegishli emas.");

            var paymentSession = await _paymentSessionRepo.GetBySessionIdAsync(sessionId);
            if (paymentSession is null)
                return GenericDto<List<HoldInvoiceItemDto>>.Success(new List<HoldInvoiceItemDto>());

            var invoices = await _invoiceRepo.GetByPaymentSessionAsync(paymentSession.Id);
            return GenericDto<List<HoldInvoiceItemDto>>.Success(
                invoices.Select(PaymentSessionService.MapItem).ToList());
        }

        private static HoldInvoiceResultDto MapResult(HoldInvoiceEntity invoice, string message) => new()
        {
            InvoiceId = invoice.Id,
            SequenceNo = invoice.SequenceNo,
            Status = invoice.Status,
            ProviderReceiptId = invoice.ProviderReceiptId,
            AmountTiyin = invoice.AmountTiyin,
            AmountUzs = Money.ToUzs(invoice.AmountTiyin),
            ResultMessage = message
        };

        private Task LogStepAsync(
            HoldInvoiceEntity invoice,
            PaymentSessionEntity paymentSession,
            HoldInvoiceStepType stepType,
            PaymentStepStatus status,
            string? requestPayload = null,
            string? responsePayload = null,
            string? message = null)
            => _invoiceRepo.AddStepAsync(new HoldInvoiceStepEntity
            {
                HoldInvoiceId = invoice.Id,
                PaymentSessionId = paymentSession.Id,
                SessionId = paymentSession.SessionId,
                MerchantId = paymentSession.MerchantId,
                DeviceId = paymentSession.DeviceId,
                UserId = paymentSession.UserId,
                StepType = stepType,
                Status = status,
                RequestPayload = EnsureJson(requestPayload),
                ResponsePayload = EnsureJson(responsePayload),
                Message = message,
                CorrelationId = paymentSession.CorrelationId
            });

        /// <summary>jsonb ustuniga faqat valid JSON yozish mumkin — bo'sh/buzuq bo'lsa string sifatida o'raladi.</summary>
        private static string? EnsureJson(string? payload)
        {
            if (string.IsNullOrWhiteSpace(payload)) return null;
            try
            {
                using var _ = JsonDocument.Parse(payload);
                return payload;
            }
            catch (JsonException)
            {
                return JsonSerializer.Serialize(payload);
            }
        }
    }
}
