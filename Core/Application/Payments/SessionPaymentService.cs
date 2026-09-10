using Domain.Dtos.Base;
using Domain.Dtos.Payment;
using Domain.Dtos.PaymentSession;
using Domain.Entities;
using Domain.Enums;
using Domain.Guards;
using Domain.Helpers;
using Domain.Interfaces;
using Domain.Options;
using Domain.Payments;
using Domain.Repositories;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

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
        private readonly IProductRepository _productRepo;
        private readonly IMerchantRepository _merchantRepo;
        private readonly ICustomerCardService _cards;
        private readonly PaymentOptions _options;
        private readonly ILogger<SessionPaymentService> _logger;

        public SessionPaymentService(
            ISessionRepository sessionRepo,
            IPaymentSessionRepository paymentSessionRepo,
            IPaymentIntentRepository intentRepo,
            IPaymentSessionService paymentSessions,
            ISessionPaymentStrategyResolver resolver,
            IProductRepository productRepo,
            IMerchantRepository merchantRepo,
            ICustomerCardService cards,
            IOptions<PaymentOptions> options,
            ILogger<SessionPaymentService> logger)
        {
            _sessionRepo = sessionRepo;
            _paymentSessionRepo = paymentSessionRepo;
            _intentRepo = intentRepo;
            _paymentSessions = paymentSessions;
            _resolver = resolver;
            _productRepo = productRepo;
            _merchantRepo = merchantRepo;
            _cards = cards;
            _options = options.Value;
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

        // ── To'lov oynasi (mahsulot tanlangandan keyin) ────────

        public async Task<GenericDto<PaymentCheckoutDto>> GetCheckoutAsync(PaymentCheckoutQueryDto query)
        {
            var session = await _sessionRepo.GetByIdAsync(query.SessionId);

            var stop = StopFactorCheck.For(StopActions.PaymentCheckout)
                .StopIf(session is null, StopFactors.Session.NotFound)
                .StopIf(() => session!.UserId != query.UserId, StopFactors.Session.NotOwned)
                .StopIf(() => session!.Status == SessionStatus.Closed, StopFactors.Session.Closed)
                .Result();

            if (stop is not null)
                return GenericDto<PaymentCheckoutDto>.Blocked(stop);

            // Kontekst connect'da yaratiladi; yo'q bo'lsa shu yerda (usul merchant sozlamasidan).
            var ensured = await _paymentSessions.EnsureForSessionAsync(query.SessionId);
            if (!ensured.IsSuccess)
                return GenericDto<PaymentCheckoutDto>.Blocked(ensured.Stop!);

            var ps = ensured.PaymentSession!;

            var strategy = _resolver.ForMethod(ps.Method);
            if (strategy is null)
                return GenericDto<PaymentCheckoutDto>.Blocked(StopFactors.Payment.MethodNotAvailable(ps.Method));

            // Usul nima talab qilishini STRATEGIYA aytadi — bu yerda usul nomi bo'yicha shart yo'q.
            var prereq = await strategy.GetPrerequisitesAsync(ps, query.UserId);

            var merchant = await _merchantRepo.GetByIdAsync(ps.MerchantId);
            var intents = await _intentRepo.GetByPaymentSessionAsync(ps.Id);
            var availableTiyin = Math.Max(0, ps.FundedTiyin - ps.ConsumedTiyin);
            var availableUzs = Money.ToUzs(availableTiyin);
            var activeIntentCount = await _intentRepo.CountActiveForPaymentSessionAsync(ps.Id);

            var dto = new PaymentCheckoutDto
            {
                SessionId = query.SessionId,
                PaymentSessionId = ps.Id,
                PaymentSessionStatus = ps.Status,
                Method = ps.Method,
                Kind = strategy.Profile.Kind,
                IsHold = strategy.Profile.Kind == PaymentIntentKind.Hold,
                MerchantId = ps.MerchantId,
                MerchantName = merchant?.CompanyName,

                FundedTiyin = ps.FundedTiyin,
                ConsumedTiyin = ps.ConsumedTiyin,
                AvailableTiyin = availableTiyin,
                AvailableUzs = availableUzs,

                MinAmountUzs = _options.MinAmountUzs,
                MaxActiveIntents = _options.MaxIntentsPerSession,
                ActiveIntentCount = activeIntentCount,

                RequiresCard = prereq.RequiresCard,
                HasUsableCard = prereq.HasUsableCard,
                SuggestedCardId = prereq.SuggestedCardId,
                RequiresPhone = prereq.RequiresPhone,
                Phone = prereq.Phone,
                RequiresCheckout = prereq.RequiresCheckout,
                Hint = prereq.Hint,
                Intents = intents.Select(PaymentStrategyBase.MapItem).ToList()
            };

            // Karta bilan ishlaydigan usulda ro'yxatni ham qaytaramiz — mijoz to'lov oynasidan
            // chiqmasdan karta tanlaydi yoki yangisini qo'shadi (token merchant kassasiga bog'langan).
            if (prereq.RequiresCard)
            {
                var cards = await _cards.GetMyAsync(query.UserId, ps.MerchantId);
                if (cards.IsSuccess)
                    dto.Cards = cards.Result!;
            }

            // Yangi to'lov faqat kontekst ochiq bo'lganda va usul sharti bajarilganda mumkin.
            var contextOpen = ps.Status == PaymentSessionStatus.Active
                              && session!.Status is SessionStatus.Connected or SessionStatus.InProcess;

            dto.CanCreateIntent = contextOpen && prereq.IsReady
                                  && activeIntentCount < _options.MaxIntentsPerSession;

            dto.MissingRequirement = !contextOpen
                ? (session!.Status == SessionStatus.Paused
                    ? StopFactors.Session.Paused.Message
                    : StopFactors.Session.PaymentContextClosed.Message)
                : !prereq.IsReady
                    ? prereq.MissingRequirement
                    : activeIntentCount >= _options.MaxIntentsPerSession
                        ? StopFactors.Payment.IntentLimit(_options.MaxIntentsPerSession).Message
                        : null;

            ApplyProduct(dto, query, await ResolveProductAsync(query, session!));

            return GenericDto<PaymentCheckoutDto>.Success(dto);
        }

        /// <summary>Tanlangan mahsulot — faqat shu sessiyaning qurilmasiniki.</summary>
        private async Task<ProductEntity?> ResolveProductAsync(PaymentCheckoutQueryDto query, SessionEntity session)
        {
            if (query.ProductId is null)
                return null;

            var product = await _productRepo.GetByIdAsync(query.ProductId.Value);
            if (product is null || !product.IsActive || product.DeviceId != session.DeviceId)
            {
                _logger.LogInformation(
                    "[PAY] Checkout: productId={ProductId} sessiya qurilmasiga mos emas (sessionId={SessionId}).",
                    query.ProductId, session.Id);
                return null;
            }

            return product;
        }

        /// <summary>
        /// Mahsulot narxidan kelib chiqib: qancha turadi, mavjud mablag' yetadimi va
        /// ilova to'lov maydoniga qaysi summani yozishi kerak.
        /// </summary>
        private void ApplyProduct(PaymentCheckoutDto dto, PaymentCheckoutQueryDto query, ProductEntity? product)
        {
            if (product is null)
            {
                // Mahsulotsiz ham oyna ochiladi — mijoz shunchaki summa ajratmoqchi bo'lishi mumkin.
                dto.SuggestedAmountUzs = dto.AvailableTiyin > 0 ? 0m : _options.MinAmountUzs;
                return;
            }

            var maxByFunds = product.Price > 0 ? dto.AvailableUzs / product.Price : 0m;

            dto.Product = new CheckoutProductDto
            {
                ProductId = product.Id,
                Name = product.Name,
                Type = product.Type.ToString(),
                Unit = product.Unit.ToString(),
                PricePerUnit = product.Price,
                MaxAmountByFunds = decimal.Round(maxByFunds, 2, MidpointRounding.ToZero)
            };

            var requested = query.RequestedAmount ?? 0m;
            if (requested <= 0)
            {
                // Miqdor tanlanmagan — mablag' bo'lmasa minimal to'lovni taklif qilamiz.
                dto.SuggestedAmountUzs = dto.AvailableTiyin > 0 ? 0m : _options.MinAmountUzs;
                dto.CanStartNow = dto.AvailableTiyin > 0;
                return;
            }

            dto.RequestedAmount = requested;
            dto.EstimatedCostUzs = decimal.Round(requested * product.Price, 2, MidpointRounding.AwayFromZero);

            var missing = dto.EstimatedCostUzs - dto.AvailableUzs;
            dto.AmountToFundUzs = missing > 0 ? missing : 0m;
            dto.CanStartNow = dto.AmountToFundUzs == 0m && dto.AvailableTiyin > 0;

            // To'lov kerak bo'lsa — yetmayotgan qismni (minimal summadan kam bo'lmagan holda) taklif qilamiz.
            dto.SuggestedAmountUzs = dto.AmountToFundUzs > 0
                ? Math.Max(dto.AmountToFundUzs, _options.MinAmountUzs)
                : 0m;
        }

        public async Task<SessionPaymentSnapshotDto?> GetSnapshotAsync(long sessionId)
        {
            var ps = await _paymentSessionRepo.GetBySessionIdAsync(sessionId);
            if (ps is null)
                return null;

            var strategy = _resolver.ForMethod(ps.Method);
            var intents = await _intentRepo.GetByPaymentSessionAsync(ps.Id);
            var available = Math.Max(0, ps.FundedTiyin - ps.ConsumedTiyin);

            var pending = intents.FirstOrDefault(i =>
                i.Status is PaymentIntentStatus.Created or PaymentIntentStatus.WaitingForConfirmation);

            return new SessionPaymentSnapshotDto
            {
                PaymentSessionId = ps.Id,
                Status = ps.Status,
                Method = ps.Method,
                Kind = strategy?.Profile.Kind ?? PaymentIntentKind.Hold,
                IsHold = strategy?.Profile.Kind == PaymentIntentKind.Hold,
                MerchantId = ps.MerchantId,
                FundedTiyin = ps.FundedTiyin,
                ConsumedTiyin = ps.ConsumedTiyin,
                AvailableTiyin = available,
                AvailableUzs = Money.ToUzs(available),
                ActiveIntentCount = intents.Count(i => !PaymentIntentStateMachine.IsTerminal(i.Status)),
                RequiresUserAction = pending is not null,
                CheckoutUrl = pending?.CheckoutUrl
            };
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
