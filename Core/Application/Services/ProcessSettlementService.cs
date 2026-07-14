using Domain.Enums;
using Domain.Interfaces;
using Domain.Repositories;

namespace Application.Services
{
    /// <summary>
    /// Process hisob-kitobini funding manbasiga qarab yo'naltiradi. Yagona chaqiruv nuqtasi —
    /// ProcessService/SessionService'dagi barcha settlement joylari shu orqali o'tadi.
    /// </summary>
    public class ProcessSettlementService : IProcessSettlementService
    {
        private readonly IProductProcessRepository _processRepo;
        private readonly IBillingService _billing;
        private readonly IHoldSettlementService _holdSettlement;

        public ProcessSettlementService(
            IProductProcessRepository processRepo,
            IBillingService billing,
            IHoldSettlementService holdSettlement)
        {
            _processRepo = processRepo;
            _billing = billing;
            _holdSettlement = holdSettlement;
        }

        public async Task<decimal> SettleAsync(long processId)
        {
            var process = await _processRepo.GetByIdAsync(processId);
            if (process is null)
                return 0m;

            return process.FundingSource == ProcessFundingSource.HoldBalance
                ? await _holdSettlement.ConsumeForProcessAsync(processId)
                : await _billing.DeductForProcessAsync(processId);
        }
    }
}
