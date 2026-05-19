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
        /// Atomic SET — race-safe usulda GivenAmount ni cumulative qiymatga o'rnatish.
        /// Qurilma har telemetry'da jami bergan miqdorni (cumulative) yuboradi, shuning uchun delta
        /// emas, to'g'ridan-to'g'ri o'rnatish kerak. Faqat aktiv (Started/InProcess) jarayonlarda va
        /// kirgan sequence eski sequence'dan kattaroq bo'lsa bajariladi (idempotency).
        /// Qaytariladigan qiymat: o'zgartirilgan satrlar soni (0 yoki 1).
        /// </summary>
        Task<int> SetGivenAmountAsync(long processId, decimal totalGiven, long sequence);
    }
}
