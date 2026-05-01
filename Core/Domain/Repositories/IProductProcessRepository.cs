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
        /// Atomic increment — race-safe usulda GivenAmount ni oshirish.
        /// Faqat aktiv (Started/InProcess) jarayonlarda ishlaydi.
        /// Qaytariladigan qiymat: o'zgartirilgan satrlar soni (0 yoki 1).
        /// </summary>
        Task<int> IncrementGivenAmountAsync(long processId, decimal delta, long sequence);
    }
}
