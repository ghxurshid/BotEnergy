using Domain.Enums;
using Domain.Interfaces;
using Domain.Payments;
using Domain.Repositories;

namespace Application.Services
{
    /// <summary>
    /// Process hisob-kitobini to'g'ri manbaga yo'naltiradi. Yagona chaqiruv nuqtasi —
    /// ProcessService/SessionService'dagi barcha settlement joylari shu orqali o'tadi.
    ///
    /// Yangi jarayonlar har doim sessiya to'lov konteksti orqali hisoblanadi (strategiya
    /// o'zi tanlanadi). <c>InternalBalance</c> — faqat ESKI yozuvlar uchun qolgan yo'l:
    /// biznes mantig'ida ichki balansdan foydalanilmaydi.
    /// </summary>
    public class ProcessSettlementService : IProcessSettlementService
    {
        private readonly IProductProcessRepository _processRepo;
        private readonly IBillingService _billing;
        private readonly ISessionPaymentService _payments;

        public ProcessSettlementService(
            IProductProcessRepository processRepo,
            IBillingService billing,
            ISessionPaymentService payments)
        {
            _processRepo = processRepo;
            _billing = billing;
            _payments = payments;
        }

        public async Task<decimal> SettleAsync(long processId)
        {
            var process = await _processRepo.GetByIdAsync(processId);
            if (process is null)
                return 0m;

            return process.FundingSource == ProcessFundingSource.InternalBalance
                ? await _billing.DeductForProcessAsync(processId)
                : await _payments.ConsumeForProcessAsync(processId);
        }
    }
}
