using Domain.Auth;
using Domain.Dtos.Base;
using Domain.Dtos.PaymentSession;
using Domain.Entities;
using Domain.Enums;
using Domain.Helpers;
using Domain.Interfaces;
using Domain.Repositories;
using Domain.Guards;

namespace Application.Services
{
    /// <summary>
    /// Operator hold invoice boshqaruvi. Payme'ni chaqirmaydi — maqsad holat qo'yadi
    /// (NextAttemptAt=now), SessionApi watcher bajaradi. Har amal OperatorAction audit.
    /// </summary>
    public class PaymentIntentAdminService : IPaymentIntentAdminService
    {
        private readonly IPaymentIntentRepository _intentRepo;
        private readonly IPaymentSessionRepository _paymentSessionRepo;

        public PaymentIntentAdminService(
            IPaymentIntentRepository intentRepo,
            IPaymentSessionRepository paymentSessionRepo)
        {
            _intentRepo = intentRepo;
            _paymentSessionRepo = paymentSessionRepo;
        }

        public async Task<GenericDto<List<PaymentIntentAdminItemDto>>> ListAsync(
            int skip, int take, long? merchantId, long? sessionId,
            PaymentIntentStatus? status, PaymentMethod? method, DateTime? from, DateTime? to, AccessScope scope)
        {
            // Merchant-scoped operator faqat o'z merchantini ko'radi.
            var effectiveMerchant = scope.IsManage ? merchantId : scope.MerchantId;
            if (!scope.IsManage && effectiveMerchant is null)
                return GenericDto<List<PaymentIntentAdminItemDto>>.Success(new List<PaymentIntentAdminItemDto>());

            var items = await _intentRepo.ListAllAsync(skip, take, effectiveMerchant, sessionId, status, method, from, to);
            return GenericDto<List<PaymentIntentAdminItemDto>>.Success(items.Select(MapItem).ToList());
        }

        public async Task<GenericDto<PaymentIntentAdminItemDto>> GetByIdAsync(long intentId, AccessScope scope)
        {
            var (intent, ps, stop) = await LoadAsync(intentId, scope);
            if (stop is not null) return GenericDto<PaymentIntentAdminItemDto>.Blocked(stop);
            return GenericDto<PaymentIntentAdminItemDto>.Success(MapItem(intent!));
        }

        public async Task<GenericDto<List<PaymentIntentStepItemDto>>> GetStepsAsync(long intentId, AccessScope scope)
        {
            var (intent, ps, stop) = await LoadAsync(intentId, scope);
            if (stop is not null) return GenericDto<List<PaymentIntentStepItemDto>>.Blocked(stop);

            var steps = await _intentRepo.GetStepsAsync(intentId);
            return GenericDto<List<PaymentIntentStepItemDto>>.Success(steps.Select(MapStep).ToList());
        }

        public async Task<GenericDto<PaymentIntentAdminItemDto>> ForceCaptureAsync(long intentId, PaymentIntentOperatorActionDto dto, long adminUserId, AccessScope scope)
        {
            var (intent, ps, stop) = await LoadAsync(intentId, scope);
            if (stop is not null) return GenericDto<PaymentIntentAdminItemDto>.Blocked(stop);

            var captureTiyin = dto.AmountUzs.HasValue ? Money.ToTiyin(dto.AmountUzs.Value) : intent!.ConsumedTiyin;
            if (captureTiyin <= 0)
                return GenericDto<PaymentIntentAdminItemDto>.Blocked(StopFactors.Payment.AmountNotPositive);

            if (!await _intentRepo.TryTransitionAsync(intentId, PaymentIntentStatus.SettlePending,
                    captureAmountTiyin: captureTiyin, nextAttemptAt: DateTime.Now))
                return GenericDto<PaymentIntentAdminItemDto>.Blocked(
                    StopFactors.Payment.IntentTransitionNotAllowed(intent!.Status, "capture"));

            await LogOperatorAsync(intent!, ps!, adminUserId, $"CAPTURE {captureTiyin} tiyin — {dto.Reason}");
            return await ReloadItemAsync(intentId);
        }

        public async Task<GenericDto<PaymentIntentAdminItemDto>> ForceRefundAsync(long intentId, PaymentIntentOperatorActionDto dto, long adminUserId, AccessScope scope)
        {
            var (intent, ps, stop) = await LoadAsync(intentId, scope);
            if (stop is not null) return GenericDto<PaymentIntentAdminItemDto>.Blocked(stop);

            if (!await _intentRepo.TryTransitionAsync(intentId, PaymentIntentStatus.RefundPending, nextAttemptAt: DateTime.Now))
                return GenericDto<PaymentIntentAdminItemDto>.Blocked(
                    StopFactors.Payment.IntentTransitionNotAllowed(intent!.Status, "refund"));

            // Hold balansdan chiqarib qo'yamiz (agar hali hisoblangan bo'lsa).
            if (intent!.Status is PaymentIntentStatus.Funded && intent.ConsumedTiyin == 0)
                await _paymentSessionRepo.TryAddFundedBalanceAsync(ps!.Id, -intent.AmountTiyin);

            await LogOperatorAsync(intent, ps!, adminUserId, $"REFUND — {dto.Reason}");
            return await ReloadItemAsync(intentId);
        }

        public async Task<GenericDto<PaymentIntentAdminItemDto>> ForceCancelAsync(long intentId, PaymentIntentOperatorActionDto dto, long adminUserId, AccessScope scope)
        {
            var (intent, ps, stop) = await LoadAsync(intentId, scope);
            if (stop is not null) return GenericDto<PaymentIntentAdminItemDto>.Blocked(stop);

            if (!PaymentIntentStateMachine.CanTransition(intent!.Status, PaymentIntentStatus.Cancelled))
                return GenericDto<PaymentIntentAdminItemDto>.Blocked(
                    StopFactors.Payment.IntentTransitionNotAllowed(
                        intent.Status, "cancel", "Hold bo'lsa Refund ishlating."));

            if (!await _intentRepo.TryTransitionAsync(intentId, PaymentIntentStatus.Cancelled))
                return GenericDto<PaymentIntentAdminItemDto>.Blocked(StopFactors.Payment.IntentStateChanged);

            await LogOperatorAsync(intent, ps!, adminUserId, $"CANCEL — {dto.Reason}");
            return await ReloadItemAsync(intentId);
        }

        public async Task<GenericDto<PaymentIntentAdminItemDto>> RetryAsync(long intentId, PaymentIntentOperatorActionDto dto, long adminUserId, AccessScope scope)
        {
            var (intent, ps, stop) = await LoadAsync(intentId, scope);
            if (stop is not null) return GenericDto<PaymentIntentAdminItemDto>.Blocked(stop);

            if (intent!.Status != PaymentIntentStatus.Failed)
                return GenericDto<PaymentIntentAdminItemDto>.Blocked(StopFactors.Payment.IntentRetryNotApplicable);

            // Consume bo'lgan bo'lsa capture, aks holda refund maqsadiga qaytaramiz.
            var target = intent.ConsumedTiyin > 0 ? PaymentIntentStatus.SettlePending : PaymentIntentStatus.RefundPending;
            var captureAmount = target == PaymentIntentStatus.SettlePending ? intent.ConsumedTiyin : (long?)null;

            if (!await _intentRepo.TryTransitionAsync(intentId, target,
                    captureAmountTiyin: captureAmount, nextAttemptAt: DateTime.Now))
                return GenericDto<PaymentIntentAdminItemDto>.Blocked(StopFactors.Payment.IntentStateChanged);

            await LogOperatorAsync(intent, ps!, adminUserId, $"RETRY → {target} — {dto.Reason}");
            return await ReloadItemAsync(intentId);
        }

        // ── Yordamchi ─────────────────────────────────────────────

        /// <summary>
        /// Har bir operator amali uchun umumiy uch to'siq: to'lov bor, konteksti bor,
        /// caller doirasida. Topilsa <c>stop</c> to'ldiriladi va amal bajarilmaydi.
        /// </summary>
        private async Task<(PaymentIntentEntity? intent, PaymentSessionEntity? ps, StopFactor? stop)> LoadAsync(
            long intentId, AccessScope scope)
        {
            var intent = await _intentRepo.GetByIdAsync(intentId);
            if (intent is null)
                return (null, null, StopFactors.Payment.IntentNotFound);

            var ps = await _paymentSessionRepo.GetByIdAsync(intent.PaymentSessionId);
            if (ps is null)
                return (null, null, StopFactors.Payment.PaymentContextNotFound);

            if (!scope.CanAccessMerchant(ps.MerchantId))
                return (null, null, StopFactors.Payment.IntentNotOwned);

            return (intent, ps, null);
        }

        private async Task<GenericDto<PaymentIntentAdminItemDto>> ReloadItemAsync(long intentId)
        {
            var fresh = await _intentRepo.GetByIdAsync(intentId);
            return GenericDto<PaymentIntentAdminItemDto>.Success(MapItem(fresh!));
        }

        private Task LogOperatorAsync(PaymentIntentEntity intent, PaymentSessionEntity ps, long adminUserId, string message)
            => _intentRepo.AddStepAsync(new PaymentIntentStepEntity
            {
                PaymentIntentId = intent.Id,
                PaymentSessionId = ps.Id,
                SessionId = ps.SessionId,
                MerchantId = ps.MerchantId,
                DeviceId = ps.DeviceId,
                UserId = ps.UserId,
                StepType = PaymentIntentStepType.OperatorAction,
                Status = PaymentStepStatus.Info,
                Message = $"[admin#{adminUserId}] {message}",
                CorrelationId = ps.CorrelationId
            });

        private static PaymentIntentAdminItemDto MapItem(PaymentIntentEntity i) => new()
        {
            IntentId = i.Id,
            PaymentSessionId = i.PaymentSessionId,
            SessionId = i.PaymentSession?.SessionId ?? 0,
            MerchantId = i.PaymentSession?.MerchantId ?? 0,
            DeviceId = i.PaymentSession?.DeviceId ?? 0,
            UserId = i.PaymentSession?.UserId ?? i.CreatedByUserId,
            SequenceNo = i.SequenceNo,
            Status = i.Status,
            Method = i.Method,
            Kind = i.Kind,
            AmountTiyin = i.AmountTiyin,
            ConsumedTiyin = i.ConsumedTiyin,
            CaptureAmountTiyin = i.CaptureAmountTiyin,
            ProviderReceiptId = i.ProviderReceiptId,
            ProviderTransactionId = i.ProviderTransactionId,
            ProviderState = i.ProviderState,
            AttemptCount = i.AttemptCount,
            NextAttemptAt = i.NextAttemptAt,
            FailureReason = i.FailureReason,
            CreatedDate = i.CreatedDate,
            FundedAt = i.FundedAt,
            SettledAt = i.SettledAt,
            UnrefundedRemainderTiyin = UnrefundedRemainder(i)
        };

        /// <summary>
        /// Prepaid + qisman ishlatilgan = qaytarilmaydigan qoldiq (Payme chekni qisman qaytarmaydi).
        /// Hold usulida bunday qoldiq bo'lmaydi — confirm_hold faqat ishlatilganini yechadi.
        /// </summary>
        private static long UnrefundedRemainder(PaymentIntentEntity i)
            => i.Kind == PaymentIntentKind.Charge
               && i.Status is PaymentIntentStatus.SettlePending or PaymentIntentStatus.Settled
               && i.ConsumedTiyin < i.AmountTiyin
                ? i.AmountTiyin - i.ConsumedTiyin
                : 0;

        private static PaymentIntentStepItemDto MapStep(PaymentIntentStepEntity s) => new()
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
