namespace Domain.Interfaces
{
    /// <summary>
    /// Process yakuniy hisob-kitobini funding manbasiga qarab yo'naltiradi:
    /// InternalBalance → IBillingService.DeductForProcessAsync (legacy),
    /// HoldBalance → IHoldSettlementService.ConsumeForProcessAsync (FIFO hold consume).
    /// Ikkala yo'l ham bir xil TryClaimBalanceDeductionAsync claim'idan o'tadi.
    /// </summary>
    public interface IProcessSettlementService
    {
        /// <summary>Yechilgan/consume qilingan summa (UZS).</summary>
        Task<decimal> SettleAsync(long processId);
    }
}
