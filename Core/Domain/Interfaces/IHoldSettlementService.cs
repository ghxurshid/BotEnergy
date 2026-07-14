namespace Domain.Interfaces
{
    /// <summary>
    /// Hold invoice moliyaviy yakunlash mexanizmi. SessionApi-only.
    ///
    /// Mas'uliyatlar:
    ///  - dispense'ning hold balansidan FIFO consume qilinishi;
    ///  - sessiya yopilishida har invoice'ga maqsad holat qo'yish (capture/refund);
    ///  - watcher tick'lari: Payme polling + capture/refund ijrosi (retry/backoff bilan);
    ///  - barcha invoice'lar terminal bo'lgach Settling sessiyani yopish.
    /// </summary>
    public interface IHoldSettlementService
    {
        /// <summary>Sessiyaning ishlatilishi mumkin hold balansi (tiyin). 0 — hold funding yo'q.</summary>
        Task<long> GetAvailableHoldTiyinAsync(long sessionId);

        /// <summary>
        /// Process narxini hold invoice'lardan FIFO tartibda yeydi
        /// (BillingService.DeductForProcessAsync'ning hold-twin'i, o'sha claim bayrog'i bilan).
        /// Qaytadi: yechilgan summa (UZS).
        /// </summary>
        Task<decimal> ConsumeForProcessAsync(long processId);

        /// <summary>
        /// Sessiya yopilishida invoice'larga maqsad holat qo'yadi va payment session'ni
        /// Settling qiladi. Qaytadi: true — kutish kerak (watcher yakunlaydi),
        /// false — hold'lar yo'q yoki hammasi terminal (sessiyani darhol yopish mumkin).
        /// </summary>
        Task<bool> BeginSessionSettlementAsync(long sessionId, CancellationToken ct = default);

        /// <summary>Watcher tick: navbatdagi invoice'larni lease bilan olib Payme amallarini bajaradi.</summary>
        Task ProcessDueInvoicesAsync(string ownerId, CancellationToken ct = default);

        /// <summary>Watcher tick: barcha invoice'lari terminal bo'lgan Settling sessiyalarni yopadi.</summary>
        Task FinalizeSettledSessionsAsync(CancellationToken ct = default);
    }
}
