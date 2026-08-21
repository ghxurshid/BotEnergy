using Domain.Dtos.Base;
using Domain.Entities;

namespace Domain.Repositories
{
    public interface ICashCollectionRepository
    {
        Task<CashCollectionEntity?> GetByIdAsync(long id);

        /// <summary>
        /// Qurilmadagi tugallanmagan inkassatsiya (<c>Requested</c> yoki <c>BoxOpened</c>).
        /// Ikkinchi inkassator ayni qurilmani parallel ochib yuborishining oldini oladi.
        /// </summary>
        Task<CashCollectionEntity?> GetOpenByDeviceAsync(long deviceId);

        Task<CashCollectionEntity> CreateAsync(CashCollectionEntity collection);
        Task<CashCollectionEntity> UpdateAsync(CashCollectionEntity collection);

        /// <summary>Audit tarixi — merchant scope va sana bo'yicha filtrlanadi.</summary>
        Task<PagedResult<CashCollectionEntity>> GetAllAsync(
            PaginationParams param,
            long? merchantId = null,
            long? deviceId = null,
            long? incassatorUserId = null);
    }
}
