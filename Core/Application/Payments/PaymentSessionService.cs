using Domain.Dtos.Base;
using Domain.Dtos.PaymentSession;
using Domain.Entities;
using Domain.Enums;
using Domain.Guards;
using Domain.Helpers;
using Domain.Interfaces;
using Domain.Payments;
using Domain.Repositories;
using Microsoft.Extensions.Logging;

namespace Application.Payments
{
    /// <summary>
    /// To'lov konteksti (PaymentSession) boshqaruvi. Sessiya uchun to'lov usuli SHU YERDA
    /// bir marta tanlanadi — merchant runtime sozlamasidan — va kontekstga qotiriladi.
    /// </summary>
    public class PaymentSessionService : IPaymentSessionService
    {
        private readonly IPaymentSessionRepository _paymentSessionRepo;
        private readonly IPaymentIntentRepository _intentRepo;
        private readonly ISessionRepository _sessionRepo;
        private readonly IDeviceRepository _deviceRepo;
        private readonly IMerchantRepository _merchantRepo;
        private readonly ISessionPaymentStrategyResolver _resolver;
        private readonly ILogger<PaymentSessionService> _logger;

        public PaymentSessionService(
            IPaymentSessionRepository paymentSessionRepo,
            IPaymentIntentRepository intentRepo,
            ISessionRepository sessionRepo,
            IDeviceRepository deviceRepo,
            IMerchantRepository merchantRepo,
            ISessionPaymentStrategyResolver resolver,
            ILogger<PaymentSessionService> logger)
        {
            _paymentSessionRepo = paymentSessionRepo;
            _intentRepo = intentRepo;
            _sessionRepo = sessionRepo;
            _deviceRepo = deviceRepo;
            _merchantRepo = merchantRepo;
            _resolver = resolver;
            _logger = logger;
        }

        public async Task<PaymentSessionEntity> CreateForSessionAsync(
            long sessionId, long deviceId, long userId, long merchantId)
        {
            // Idempotent: session_id unique — takror connect'da mavjudini qaytaramiz.
            var existing = await _paymentSessionRepo.GetBySessionIdAsync(sessionId);
            if (existing is not null)
                return existing;

            var method = await ResolveMethodAsync(merchantId);

            var paymentSession = new PaymentSessionEntity
            {
                SessionId = sessionId,
                DeviceId = deviceId,
                UserId = userId,
                MerchantId = merchantId,
                Method = method
            };

            paymentSession = await _paymentSessionRepo.CreateAsync(paymentSession);

            _logger.LogInformation(
                "[PAY-SESSION] Yaratildi paymentSessionId={PaymentSessionId} sessionId={SessionId} " +
                "merchantId={MerchantId} method={Method}",
                paymentSession.Id, sessionId, merchantId, method);

            return paymentSession;
        }

        public async Task<EnsurePaymentSessionResult> EnsureForSessionAsync(long sessionId)
        {
            var existing = await _paymentSessionRepo.GetBySessionIdAsync(sessionId);
            if (existing is not null)
                return EnsurePaymentSessionResult.Ok(existing);

            var session = await _sessionRepo.GetByIdAsync(sessionId);
            if (session is null)
                return EnsurePaymentSessionResult.Blocked(StopFactors.Session.NotFound);
            if (session.DeviceId is null)
                return EnsurePaymentSessionResult.Blocked(StopFactors.Device.NotAttachedToSession);

            var device = await _deviceRepo.GetByIdAsync(session.DeviceId.Value);
            if (device?.Station is null)
                return EnsurePaymentSessionResult.Blocked(StopFactors.Device.NoStation);

            var created = await CreateForSessionAsync(
                sessionId, device.Id, session.UserId, device.Station.MerchantId);

            return EnsurePaymentSessionResult.Ok(created);
        }

        public async Task<GenericDto<PaymentSessionDto>> GetForSessionAsync(long sessionId, long userId)
        {
            var session = await _sessionRepo.GetByIdAsync(sessionId);
            if (session is null)
                return GenericDto<PaymentSessionDto>.Blocked(StopFactors.Session.NotFound);
            if (session.UserId != userId)
                return GenericDto<PaymentSessionDto>.Blocked(StopFactors.Session.NotOwned);

            var paymentSession = await _paymentSessionRepo.GetBySessionIdAsync(sessionId);
            if (paymentSession is null)
                return GenericDto<PaymentSessionDto>.Blocked(StopFactors.Session.NoPaymentContext);

            var intents = await _intentRepo.GetByPaymentSessionAsync(paymentSession.Id);
            var available = paymentSession.FundedTiyin - paymentSession.ConsumedTiyin;

            return GenericDto<PaymentSessionDto>.Success(new PaymentSessionDto
            {
                PaymentSessionId = paymentSession.Id,
                SessionId = sessionId,
                Status = paymentSession.Status,
                Method = paymentSession.Method,
                MerchantId = paymentSession.MerchantId,
                FundedTiyin = paymentSession.FundedTiyin,
                ConsumedTiyin = paymentSession.ConsumedTiyin,
                AvailableTiyin = available,
                AvailableUzs = Money.ToUzs(available),
                Intents = intents.Select(PaymentStrategyBase.MapItem).ToList()
            });
        }

        /// <summary>
        /// Merchant sozlamasi bo'yicha usulni tanlaydi. Merchant topilmasa global default —
        /// sessiya to'lovsiz qolib ketmasligi uchun (kontekstsiz jarayon ham boshlanmaydi).
        /// </summary>
        private async Task<PaymentMethod> ResolveMethodAsync(long merchantId)
        {
            var merchant = await _merchantRepo.GetByIdAsync(merchantId);
            if (merchant is not null)
                return await _resolver.ResolveForMerchantAsync(merchant);

            _logger.LogWarning("[PAY-SESSION] merchantId={MerchantId} topilmadi — global default usul.", merchantId);
            return await _resolver.ResolveForMerchantAsync(new MerchantEntity
            {
                PhoneNumber = string.Empty,
                Inn = string.Empty,
                BankAccount = string.Empty,
                CompanyName = string.Empty,
                EnabledPaymentMethods = PaymentMethodFlags.None
            });
        }
    }
}
