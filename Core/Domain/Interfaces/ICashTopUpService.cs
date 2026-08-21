using Domain.Dtos.Base;
using Domain.Dtos.Cash;

namespace Domain.Interfaces
{
    /// <summary>
    /// Qurilma interfeysidagi naqd → karta oqimi.
    ///
    /// Chaqiruvchi — SessionApi'dagi MQTT handler'lar (mobil ilova bu oqimda ishtirok etmaydi)
    /// va idle/watcher fon servislari.
    /// </summary>
    public interface ICashTopUpService
    {
        /// <summary>
        /// Kartani bankda tekshiradi va naqd qabul qilish sessiyasini ochadi.
        /// To'liq PAN saqlanmaydi — sessiyaga faqat maska va token yoziladi.
        /// </summary>
        Task<GenericDto<CashSessionOpenedDto>> OpenSessionAsync(
            string serialNumber, string cardPan, CancellationToken ct = default);

        /// <summary>
        /// Bill acceptor qabul qilgan kupyurani sessiyaga qo'shadi.
        /// Takroriy <paramref name="billSeq"/> summani ikkinchi marta oshirmaydi.
        /// </summary>
        Task<GenericDto<CashSessionTotalDto>> AddBillAsync(
            string serialNumber, long cashSessionId, decimal denomination, int billSeq,
            CancellationToken ct = default);

        /// <summary>
        /// Yig'ilgan summani kartaga o'tkazadi. Bank transport xatosi bilan yiqilsa sessiya
        /// <c>PayoutFailed</c> bo'ladi va watcher qayta uriniladi — natija keyin qurilmaga push qilinadi.
        /// </summary>
        Task<GenericDto<CashSessionResultDto>> CommitAsync(
            string serialNumber, long cashSessionId, string? clientRef, CancellationToken ct = default);

        /// <summary>Sessiyani bekor qiladi — faqat hech qanday pul solinmagan bo'lsa.</summary>
        Task<GenericDto<CashSessionResultDto>> CancelAsync(
            string serialNumber, long cashSessionId, CancellationToken ct = default);

        /// <summary>
        /// Bitta <c>PayoutFailed</c> sessiyani qayta urinib ko'radi (watcher va admin uchun).
        /// </summary>
        Task<GenericDto<CashSessionResultDto>> RetryPayoutAsync(
            long cashSessionId, CancellationToken ct = default);

        /// <summary>
        /// Harakatsiz qolgan sessiyalarni yopadi: summa &gt; 0 bo'lsa avtomatik commit,
        /// aks holda <c>Expired</c>.
        /// </summary>
        Task<int> CloseIdleSessionsAsync(CancellationToken ct = default);
    }
}
