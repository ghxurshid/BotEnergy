using Domain.Dtos.Base;
using Domain.Dtos.PaymentSession;
using Domain.Enums;
using Domain.Guards;
using Domain.Interfaces;
using Domain.Payments;
using Domain.Repositories;
using Microsoft.Extensions.Logging;

namespace Application.Payments
{
    /// <summary>
    /// Sessiya to'lovining YAGONA kirish nuqtasi. SessionService/ProcessService va
    /// controllerlar faqat shu servis bilan gaplashadi — konkret strategiya (Merchant/
    /// Invoice/Subscribe) ulardan yashirin.
    ///
    /// Mas'uliyat taqsimoti:
    ///  - shu servis: sessiya darajasidagi to'siqlar + to'lov kontekstini kafolatlash + strategiya tanlash;
    ///  - strategiya: summa/limit/provider bilan bog'liq hamma narsa.
    /// </summary>
    public class SessionPaymentService : ISessionPaymentService
    {
        private readonly ISessionRepository _sessionRepo;
        private readonly IPaymentSessionRepository _paymentSessionRepo;
        private readonly IPaymentIntentRepository _intentRepo;
        private readonly IPaymentSessionService _paymentSessions;
        private readonly ISessionPaymentStrategyResolver _resolver;
        private readonly ILogger<SessionPaymentService> _logger;

        public SessionPaymentService(
            ISessionRepository sessionRepo,
            IPaymentSessionRepository paymentSessionRepo,
            IPaymentIntentRepository intentRepo,
            IPaymentSessionService paymentSessions,
            ISessionPaymentStrategyResolver resolver,
            ILogger<SessionPaymentService> logger)
        {
            _sessionRepo = sessionRepo;
            _paymentSessionRepo = paymentSessionRepo;
            _intentRepo = intentRepo;
            _paymentSessions = paymentSessions;
            _resolver = resolver;
            _logger = logger;
        }

        // ── Funding / settlement (sessiya va jarayon servislari uchun) ──

        public async Task<long> GetAvailableTiyinAsync(long sessionId)
        {
            var strategy = await _resolver.ForSessionAsync(sessionId);
            return strategy is null ? 0 : await strategy.GetAvailableTiyinAsync(sessionId);
        }

        public async Task<decimal> ConsumeForProcessAsync(long processId)
        {
            var strategy = await _resolver.ForProcessAsync(processId);
            return strategy is null ? 0m : await strategy.ConsumeForProcessAsync(processId);
        }

        public async Task<bool> BeginSessionSettlementAsync(long sessionId, CancellationToken ct = default)
        {
            var strategy = await _resolver.ForSessionAsync(sessionId);
            return strategy is not null && await strategy.BeginSessionSettlementAsync(sessionId, ct);
        }

        public async Task PublishSessionPaymentStateAsync(long sessionId, string reason, long? intentId = null)
        {
            var strategy = await _resolver.ForSessionAsync(sessionId);
            if (strategy is not null)
                await strategy.PublishSessionPaymentStateAsync(sessionId, reason, intentId);
        }

        // ── Mobil oqim ──────────────────────────────────────────────

        public async Task<GenericDto<PaymentIntentResultDto>> CreateIntentAsync(
            CreatePaymentIntentDto dto, CancellationToken ct = default)
        {
            var session = await _sessionRepo.GetByIdAsync(dto.SessionId);

            var stop = StopFactorCheck.For(StopActions.PaymentIntentCreate)
                .StopIf(session is null, StopFactors.Session.NotFound)
                .StopIf(() => session!.UserId != dto.UserId, StopFactors.Session.NotOwned)
                .StopIf(() => session!.Status == SessionStatus.Paused, StopFactors.Session.Paused)
                .StopIf(() => session!.Status == SessionStatus.Settling, StopFactors.Session.Settling)
                .StopIf(() => session!.Status == SessionStatus.Closed, StopFactors.Session.Closed)
                .StopIf(() => session!.Status is not (SessionStatus.Connected or SessionStatus.InProcess),
                        StopFactors.Session.NotConnected)
                .Result();

            if (stop is not null)
                return GenericDto<PaymentIntentResultDto>.Blocked(stop);

            // To'lov konteksti connect'da yaratiladi; yo'q bo'lsa shu yerda (usul merchant
            // sozlamasidan olinadi — runtime'da almashtirilgan bo'lsa yangisi qo'llanadi).
            var ensured = await _paymentSessions.EnsureForSessionAsync(dto.SessionId);
            if (!ensured.IsSuccess)
                return GenericDto<PaymentIntentResultDto>.Blocked(ensured.Stop!);

            var paymentSession = ensured.PaymentSession!;

            var strategy = _resolver.ForMethod(paymentSession.Method);
            if (strategy is null)
                return GenericDto<PaymentIntentResultDto>.Blocked(
                    StopFactors.Payment.MethodNotAvailable(paymentSession.Method));

            return await strategy.CreateIntentAsync(dto, ct);
        }

        public async Task<GenericDto<PaymentIntentResultDto>> CancelIntentAsync(
            long intentId, long userId, CancellationToken ct = default)
        {
            var intent = await _intentRepo.GetByIdAsync(intentId);
            if (intent is null)
                return GenericDto<PaymentIntentResultDto>.Blocked(StopFactors.Payment.IntentNotFound);

            // Intent har doim uni yaratgan strategiyaga tegishli — sessiya usuli keyin
            // o'zgargan bo'lsa ham bekor qilishni o'sha strategiya bajaradi.
            var strategy = _resolver.ForMethod(intent.Method);
            if (strategy is null)
                return GenericDto<PaymentIntentResultDto>.Blocked(
                    StopFactors.Payment.MethodNotAvailable(intent.Method));

            return await strategy.CancelIntentAsync(intentId, userId, ct);
        }

        // ── O'qish ──────────────────────────────────────────────────

        public Task<GenericDto<PaymentSessionDto>> GetForSessionAsync(long sessionId, long userId)
            => _paymentSessions.GetForSessionAsync(sessionId, userId);

        public async Task<GenericDto<List<PaymentIntentItemDto>>> GetIntentsForSessionAsync(long sessionId, long userId)
        {
            var session = await _sessionRepo.GetByIdAsync(sessionId);
            if (session is null)
                return GenericDto<List<PaymentIntentItemDto>>.Blocked(StopFactors.Session.NotFound);
            if (session.UserId != userId)
                return GenericDto<List<PaymentIntentItemDto>>.Blocked(StopFactors.Session.NotOwned);

            var ps = await _paymentSessionRepo.GetBySessionIdAsync(sessionId);
            if (ps is null)
                return GenericDto<List<PaymentIntentItemDto>>.Success(new List<PaymentIntentItemDto>());

            var intents = await _intentRepo.GetByPaymentSessionAsync(ps.Id);
            return GenericDto<List<PaymentIntentItemDto>>.Success(
                intents.Select(PaymentStrategyBase.MapItem).ToList());
        }
    }
}
