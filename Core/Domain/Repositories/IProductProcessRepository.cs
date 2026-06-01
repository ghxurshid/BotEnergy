using Domain.Entities;
using Domain.Enums;

namespace Domain.Repositories
{
    public interface IProductProcessRepository
    {
        Task<ProductProcessEntity> CreateAsync(ProductProcessEntity process);
        Task<ProductProcessEntity?> GetByIdAsync(long id);
        Task<ProductProcessEntity?> GetByIdWithSessionAsync(long id);
        Task<ProductProcessEntity?> GetActiveBySessionTokenAsync(string sessionToken);
        Task<ProductProcessEntity> UpdateAsync(ProductProcessEntity process);
        Task<bool> HasActiveProcessAsync(long sessionId);

        /// <summary>
        /// Watchdog uchun — aktiv (Started/InProcess) bo'lib turgan, lekin <paramref name="staleBefore"/>
        /// dan beri yangilanmagan (telemetry kelmagan) jarayonlar. Session+Device+User include qilinadi.
        /// Paused jarayonlar kiritilmaydi (ular session idle-timeout bilan boshqariladi).
        /// </summary>
        Task<List<ProductProcessEntity>> GetStalledProcessesAsync(DateTime staleBefore);

        /// <summary>
        /// Atomic SET — race-safe usulda GivenAmount ni cumulative qiymatga o'rnatish.
        /// Status ham bir vaqtning o'zida InProcess'ga o'tkaziladi (birinchi telemetry kelganda).
        /// Faqat aktiv (Started/InProcess) jarayonlarda va kirgan sequence eski sequence'dan
        /// kattaroq bo'lsa bajariladi (idempotency).
        /// Qaytariladigan qiymat: o'zgartirilgan satrlar soni (0 yoki 1).
        /// </summary>
        Task<int> SetGivenAmountAsync(long processId, decimal totalGiven, long sequence);

        /// <summary>
        /// Atomic process yakunlash — Status=Ended, EndReason, EndedAt, GivenAmount o'rnatiladi.
        /// Faqat hali Ended bo'lmagan jarayonlar yangilanadi (idempotent — bitta thread yutadi).
        /// </summary>
        Task<int> CompleteProcessAsync(long processId, decimal totalGiven, ProcessEndReason endReason, DateTime endedAt);

        /// <summary>
        /// Tracker'dagi entity'ni DB'dan qayta yuklash — ExecuteUpdateAsync orqali yangilangan
        /// yoki boshqa scope o'zgartirgan satrni in-memory bilan sinxronlash uchun.
        /// </summary>
        Task ReloadAsync(ProductProcessEntity process);
    }
}
