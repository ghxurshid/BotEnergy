using Domain.Dtos.Base;
using Domain.Dtos.Cash;
using Domain.Entities;
using Domain.Enums;

namespace Domain.Repositories
{
    public interface ICashSessionRepository
    {
        Task<CashSessionEntity?> GetByIdAsync(long id);

        /// <summary>
        /// Qurilmadagi ochiq sessiya (<c>Accepting</c> yoki <c>Committing</c>).
        /// Qurilmada bir vaqtda bittadan ortiq ochiq sessiya bo'lishi mumkin emas —
        /// buni partial unique index ham kafolatlaydi.
        /// </summary>
        Task<CashSessionEntity?> GetActiveByDeviceAsync(long deviceId);

        /// <summary>Takroriy commit'ni aniqlash uchun — idempotentlik kaliti bo'yicha qidiruv.</summary>
        Task<CashSessionEntity?> GetByIdempotencyKeyAsync(string idempotencyKey);

        Task<CashSessionEntity> CreateAsync(CashSessionEntity session);
        Task<CashSessionEntity> UpdateAsync(CashSessionEntity session);

        /// <summary>
        /// Kupyurani yozadi va sessiya jamini bitta atomik qadamda oshiradi.
        /// Takroriy <paramref name="billSeq"/> unique index bilan rad etiladi —
        /// bunday holda summa oshmaydi va <c>Added=false</c> qaytadi.
        /// Sessiya <c>Accepting</c> holatida bo'lmasa ham summa oshmaydi.
        /// </summary>
        Task<CashBillAddResult> TryAddBillAsync(
            long sessionId, long deviceId, string serialNumber, decimal denomination, int billSeq);

        /// <summary>
        /// Watcher uchun: qayta urinish vaqti kelgan sessiyalarni lease bilan band qiladi
        /// (<c>SKIP LOCKED</c> — parallel instansiyalar bir-birini kutmaydi).
        /// </summary>
        Task<List<CashSessionEntity>> ClaimDueAsync(string ownerId, DateTime leaseUntil, int batch);

        Task ReleaseLeaseAsync(long id, string ownerId);

        /// <summary>
        /// <paramref name="threshold"/> dan beri harakatsiz turgan ochiq sessiyalar —
        /// timeout siyosati uchun (summa &gt; 0 bo'lsa avtomatik commit, aks holda Expired).
        /// </summary>
        Task<List<CashSessionEntity>> GetIdleAsync(DateTime threshold);

        /// <summary>Admin ro'yxati — holat bo'yicha filtr (masalan faqat <c>PayoutFailed</c>).</summary>
        Task<PagedResult<CashSessionEntity>> GetAllAsync(
            PaginationParams param, long? merchantId = null, CashSessionStatus? status = null);
    }
}
