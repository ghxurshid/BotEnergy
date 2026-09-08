using Domain.Entities;
using Domain.Enums;
using Domain.Options;
using Domain.Payments;
using Domain.Repositories;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Application.Payments
{
    /// <summary>
    /// Qaysi strategiya ishlashini aniqlaydigan yagona joy.
    ///
    /// Tanlash zanjiri (birinchi mos kelgani yutadi):
    ///  1. <see cref="PaymentSessionEntity.Method"/> — sessiya ochilganda QOTIRILGAN usul;
    ///  2. mijoz so'ragan usul — merchant ruxsat etgan bo'lsa;
    ///  3. merchant sozlamasi (<see cref="MerchantEntity.DefaultPaymentMethod"/>) — RUNTIME'da almashadi;
    ///  4. global config default (<c>Payments:DefaultMethod</c>).
    /// </summary>
    public class SessionPaymentStrategyResolver : ISessionPaymentStrategyResolver
    {
        private readonly IPaymentSessionRepository _paymentSessions;
        private readonly IProductProcessRepository _processes;
        private readonly PaymentOptions _options;
        private readonly ILogger<SessionPaymentStrategyResolver> _logger;

        public IReadOnlyList<ISessionPaymentStrategy> All { get; }

        public SessionPaymentStrategyResolver(
            IEnumerable<ISessionPaymentStrategy> strategies,
            IPaymentSessionRepository paymentSessions,
            IProductProcessRepository processes,
            IOptions<PaymentOptions> options,
            ILogger<SessionPaymentStrategyResolver> logger)
        {
            All = strategies.ToList();
            _paymentSessions = paymentSessions;
            _processes = processes;
            _options = options.Value;
            _logger = logger;
        }

        public ISessionPaymentStrategy? ForMethod(PaymentMethod method)
            => All.FirstOrDefault(s => s.Profile.Method == method);

        public async Task<ISessionPaymentStrategy?> ForSessionAsync(long sessionId)
        {
            var ps = await _paymentSessions.GetBySessionIdAsync(sessionId);
            if (ps is null)
                return null;

            var strategy = ForMethod(ps.Method);
            if (strategy is null)
                _logger.LogError(
                    "[PAY] sessionId={SessionId} uchun {Method} strategiyasi ro'yxatga olinmagan — " +
                    "to'lov amallari bajarilmaydi.", sessionId, ps.Method);

            return strategy;
        }

        public async Task<ISessionPaymentStrategy?> ForProcessAsync(long processId)
        {
            var process = await _processes.GetByIdWithSessionAsync(processId);
            return process?.Session is null ? null : await ForSessionAsync(process.Session.Id);
        }

        public Task<PaymentMethod> ResolveForMerchantAsync(MerchantEntity merchant, PaymentMethod? requested = null)
        {
            // Merchant hech narsa yoqmagan bo'lsa — global default bilan ishlaymiz
            // (eski merchantlar migratsiyadan keyin ham to'xtab qolmasin).
            var allowed = merchant.EnabledPaymentMethods == PaymentMethodFlags.None
                ? _options.DefaultMethod.ToFlag()
                : merchant.EnabledPaymentMethods;

            // 1. Mijoz so'ragan usul — ruxsat etilgan, ro'yxatga olingan va sozlangan bo'lsa.
            if (requested.HasValue && IsUsable(merchant, allowed, requested.Value))
                return Task.FromResult(requested.Value);

            // 2. Merchant default'i (runtime sozlamasi).
            if (IsUsable(merchant, allowed, merchant.DefaultPaymentMethod))
                return Task.FromResult(merchant.DefaultPaymentMethod);

            // 3. Ruxsat etilganlardan ishlaydigan birinchisi. FirstOrDefault ISHLATILMAYDI:
            //    bo'sh ro'yxatda u enum'ning 0-qiymatini (Merchant) qaytarib, merchant
            //    yoqmagan usulni tanlab qo'yardi.
            foreach (var candidate in allowed.ToMethods())
            {
                if (!IsUsable(merchant, allowed, candidate)) continue;

                _logger.LogWarning(
                    "[PAY] merchantId={MerchantId}: default usul ({Default}) ishlamaydi — {Fallback} tanlandi.",
                    merchant.Id, merchant.DefaultPaymentMethod, candidate);
                return Task.FromResult(candidate);
            }

            // 4. Global default — hech narsa mos kelmasa. Sessiya baribir ochiladi, lekin
            //    birinchi to'lov urinishida aniq to'siq (credential yo'q) qaytadi.
            _logger.LogError(
                "[PAY] merchantId={MerchantId} uchun ishlaydigan to'lov usuli yo'q " +
                "(yoqilgan={Enabled}, default={Default}) — global default ({Global}) bilan ochilmoqda.",
                merchant.Id, allowed, merchant.DefaultPaymentMethod, _options.DefaultMethod);
            return Task.FromResult(_options.DefaultMethod);
        }

        /// <summary>
        /// Usul haqiqatan ishlay oladimi: merchant yoqqan + strategiya ro'yxatda + credential to'liq.
        /// Credential tekshiruvisiz sessiya buzuq usulga qotirilib, birinchi to'lovda to'xtab qolardi.
        /// </summary>
        private bool IsUsable(MerchantEntity merchant, PaymentMethodFlags allowed, PaymentMethod method)
            => allowed.Allows(method)
               && ForMethod(method) is not null
               && PaymentMethodRequirements.IsConfigured(merchant, method);
    }
}
