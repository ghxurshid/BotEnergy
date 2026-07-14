using Domain.Auth;
using Domain.Dtos.Base;
using Domain.Dtos.PaymentSession;
using Domain.Entities;
using Domain.Enums;
using Domain.Helpers;
using Domain.Interfaces;
using Domain.Repositories;

namespace Application.Services
{
    /// <summary>
    /// Operator hold invoice boshqaruvi. Payme'ni chaqirmaydi — maqsad holat qo'yadi
    /// (NextAttemptAt=now), SessionApi watcher bajaradi. Har amal OperatorAction audit.
    /// </summary>
    public class HoldInvoiceAdminService : IHoldInvoiceAdminService
    {
        private readonly IHoldInvoiceRepository _invoiceRepo;
        private readonly IPaymentSessionRepository _paymentSessionRepo;

        public HoldInvoiceAdminService(
            IHoldInvoiceRepository invoiceRepo,
            IPaymentSessionRepository paymentSessionRepo)
        {
            _invoiceRepo = invoiceRepo;
            _paymentSessionRepo = paymentSessionRepo;
        }

        public async Task<GenericDto<List<HoldInvoiceAdminItemDto>>> ListAsync(
            int skip, int take, long? merchantId, long? sessionId,
            HoldInvoiceStatus? status, DateTime? from, DateTime? to, AccessScope scope)
        {
            // Merchant-scoped operator faqat o'z merchantini ko'radi.
            var effectiveMerchant = scope.IsManage ? merchantId : scope.MerchantId;
            if (!scope.IsManage && effectiveMerchant is null)
                return GenericDto<List<HoldInvoiceAdminItemDto>>.Success(new List<HoldInvoiceAdminItemDto>());

            var items = await _invoiceRepo.ListAllAsync(skip, take, effectiveMerchant, sessionId, status, from, to);
            return GenericDto<List<HoldInvoiceAdminItemDto>>.Success(items.Select(MapItem).ToList());
        }

        public async Task<GenericDto<HoldInvoiceAdminItemDto>> GetByIdAsync(long invoiceId, AccessScope scope)
        {
            var (invoice, ps, error) = await LoadAsync(invoiceId, scope);
            if (error is not null) return GenericDto<HoldInvoiceAdminItemDto>.Error(error.Value.code, error.Value.msg);
            return GenericDto<HoldInvoiceAdminItemDto>.Success(MapItem(invoice!));
        }

        public async Task<GenericDto<List<HoldInvoiceStepItemDto>>> GetStepsAsync(long invoiceId, AccessScope scope)
        {
            var (invoice, ps, error) = await LoadAsync(invoiceId, scope);
            if (error is not null) return GenericDto<List<HoldInvoiceStepItemDto>>.Error(error.Value.code, error.Value.msg);

            var steps = await _invoiceRepo.GetStepsAsync(invoiceId);
            return GenericDto<List<HoldInvoiceStepItemDto>>.Success(steps.Select(MapStep).ToList());
        }

        public async Task<GenericDto<HoldInvoiceAdminItemDto>> ForceCaptureAsync(long invoiceId, HoldInvoiceOperatorActionDto dto, long adminUserId, AccessScope scope)
        {
            var (invoice, ps, error) = await LoadAsync(invoiceId, scope);
            if (error is not null) return GenericDto<HoldInvoiceAdminItemDto>.Error(error.Value.code, error.Value.msg);

            var captureTiyin = dto.AmountUzs.HasValue ? Money.ToTiyin(dto.AmountUzs.Value) : invoice!.ConsumedTiyin;
            if (captureTiyin <= 0)
                return GenericDto<HoldInvoiceAdminItemDto>.Error(400, "Capture summasi 0 dan katta bo'lishi kerak.");

            if (!await _invoiceRepo.TryTransitionAsync(invoiceId, HoldInvoiceStatus.CapturePending,
                    captureAmountTiyin: captureTiyin, nextAttemptAt: DateTime.Now))
                return GenericDto<HoldInvoiceAdminItemDto>.Error(409,
                    $"Joriy holat ({invoice!.Status}) capture'ga ruxsat bermaydi.");

            await LogOperatorAsync(invoice!, ps!, adminUserId, $"CAPTURE {captureTiyin} tiyin — {dto.Reason}");
            return await ReloadItemAsync(invoiceId);
        }

        public async Task<GenericDto<HoldInvoiceAdminItemDto>> ForceRefundAsync(long invoiceId, HoldInvoiceOperatorActionDto dto, long adminUserId, AccessScope scope)
        {
            var (invoice, ps, error) = await LoadAsync(invoiceId, scope);
            if (error is not null) return GenericDto<HoldInvoiceAdminItemDto>.Error(error.Value.code, error.Value.msg);

            if (!await _invoiceRepo.TryTransitionAsync(invoiceId, HoldInvoiceStatus.RefundPending, nextAttemptAt: DateTime.Now))
                return GenericDto<HoldInvoiceAdminItemDto>.Error(409,
                    $"Joriy holat ({invoice!.Status}) refund'ga ruxsat bermaydi.");

            // Hold balansdan chiqarib qo'yamiz (agar hali hisoblangan bo'lsa).
            if (invoice!.Status is HoldInvoiceStatus.Hold && invoice.ConsumedTiyin == 0)
                await _paymentSessionRepo.TryAddHoldBalanceAsync(ps!.Id, -invoice.AmountTiyin);

            await LogOperatorAsync(invoice, ps!, adminUserId, $"REFUND — {dto.Reason}");
            return await ReloadItemAsync(invoiceId);
        }

        public async Task<GenericDto<HoldInvoiceAdminItemDto>> ForceCancelAsync(long invoiceId, HoldInvoiceOperatorActionDto dto, long adminUserId, AccessScope scope)
        {
            var (invoice, ps, error) = await LoadAsync(invoiceId, scope);
            if (error is not null) return GenericDto<HoldInvoiceAdminItemDto>.Error(error.Value.code, error.Value.msg);

            if (!HoldInvoiceStateMachine.CanTransition(invoice!.Status, HoldInvoiceStatus.Cancelled))
                return GenericDto<HoldInvoiceAdminItemDto>.Error(409,
                    $"Joriy holat ({invoice.Status}) cancel'ga ruxsat bermaydi — Hold bo'lsa Refund ishlating.");

            if (!await _invoiceRepo.TryTransitionAsync(invoiceId, HoldInvoiceStatus.Cancelled))
                return GenericDto<HoldInvoiceAdminItemDto>.Error(409, "Holat o'zgargan — qayta urinib ko'ring.");

            await LogOperatorAsync(invoice, ps!, adminUserId, $"CANCEL — {dto.Reason}");
            return await ReloadItemAsync(invoiceId);
        }

        public async Task<GenericDto<HoldInvoiceAdminItemDto>> RetryAsync(long invoiceId, HoldInvoiceOperatorActionDto dto, long adminUserId, AccessScope scope)
        {
            var (invoice, ps, error) = await LoadAsync(invoiceId, scope);
            if (error is not null) return GenericDto<HoldInvoiceAdminItemDto>.Error(error.Value.code, error.Value.msg);

            if (invoice!.Status != HoldInvoiceStatus.Failed)
                return GenericDto<HoldInvoiceAdminItemDto>.Error(409, "Faqat Failed invoice retry qilinadi.");

            // Consume bo'lgan bo'lsa capture, aks holda refund maqsadiga qaytaramiz.
            var target = invoice.ConsumedTiyin > 0 ? HoldInvoiceStatus.CapturePending : HoldInvoiceStatus.RefundPending;
            var captureAmount = target == HoldInvoiceStatus.CapturePending ? invoice.ConsumedTiyin : (long?)null;

            if (!await _invoiceRepo.TryTransitionAsync(invoiceId, target,
                    captureAmountTiyin: captureAmount, nextAttemptAt: DateTime.Now))
                return GenericDto<HoldInvoiceAdminItemDto>.Error(409, "Holat o'zgargan — qayta urinib ko'ring.");

            await LogOperatorAsync(invoice, ps!, adminUserId, $"RETRY → {target} — {dto.Reason}");
            return await ReloadItemAsync(invoiceId);
        }

        // ── Yordamchi ─────────────────────────────────────────────

        private async Task<(HoldInvoiceEntity? invoice, PaymentSessionEntity? ps, (int code, string msg)? error)> LoadAsync(long invoiceId, AccessScope scope)
        {
            var invoice = await _invoiceRepo.GetByIdAsync(invoiceId);
            if (invoice is null)
                return (null, null, (404, "Invoice topilmadi."));

            var ps = await _paymentSessionRepo.GetByIdAsync(invoice.PaymentSessionId);
            if (ps is null)
                return (null, null, (404, "To'lov konteksti topilmadi."));

            if (!scope.CanAccessMerchant(ps.MerchantId))
                return (null, null, (403, "Bu invoice sizning doirangizga tegishli emas."));

            return (invoice, ps, null);
        }

        private async Task<GenericDto<HoldInvoiceAdminItemDto>> ReloadItemAsync(long invoiceId)
        {
            var fresh = await _invoiceRepo.GetByIdAsync(invoiceId);
            return GenericDto<HoldInvoiceAdminItemDto>.Success(MapItem(fresh!));
        }

        private Task LogOperatorAsync(HoldInvoiceEntity invoice, PaymentSessionEntity ps, long adminUserId, string message)
            => _invoiceRepo.AddStepAsync(new HoldInvoiceStepEntity
            {
                HoldInvoiceId = invoice.Id,
                PaymentSessionId = ps.Id,
                SessionId = ps.SessionId,
                MerchantId = ps.MerchantId,
                DeviceId = ps.DeviceId,
                UserId = ps.UserId,
                StepType = HoldInvoiceStepType.OperatorAction,
                Status = PaymentStepStatus.Info,
                Message = $"[admin#{adminUserId}] {message}",
                CorrelationId = ps.CorrelationId
            });

        private static HoldInvoiceAdminItemDto MapItem(HoldInvoiceEntity i) => new()
        {
            InvoiceId = i.Id,
            PaymentSessionId = i.PaymentSessionId,
            SessionId = i.PaymentSession?.SessionId ?? 0,
            MerchantId = i.PaymentSession?.MerchantId ?? 0,
            DeviceId = i.PaymentSession?.DeviceId ?? 0,
            UserId = i.PaymentSession?.UserId ?? i.CreatedByUserId,
            SequenceNo = i.SequenceNo,
            Status = i.Status,
            AmountTiyin = i.AmountTiyin,
            ConsumedTiyin = i.ConsumedTiyin,
            CaptureAmountTiyin = i.CaptureAmountTiyin,
            ProviderReceiptId = i.ProviderReceiptId,
            ProviderState = i.ProviderState,
            AttemptCount = i.AttemptCount,
            NextAttemptAt = i.NextAttemptAt,
            FailureReason = i.FailureReason,
            CreatedDate = i.CreatedDate,
            HoldAt = i.HoldAt,
            SettledAt = i.SettledAt
        };

        private static HoldInvoiceStepItemDto MapStep(HoldInvoiceStepEntity s) => new()
        {
            Id = s.Id,
            StepType = s.StepType,
            Status = s.Status,
            RequestPayload = s.RequestPayload,
            ResponsePayload = s.ResponsePayload,
            Message = s.Message,
            CorrelationId = s.CorrelationId,
            OccurredAt = s.OccurredAt
        };
    }
}
