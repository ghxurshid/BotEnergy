using Domain.Dtos.Base;
using Domain.Entities;
using Domain.Enums;
using Domain.Repositories;
using Microsoft.EntityFrameworkCore;
using Persistence.Context;
using Persistence.Extensions;

namespace Persistence.Repositories
{
    public class CashCollectionRepository : ICashCollectionRepository
    {
        private readonly AppDbContext _context;

        /// <summary>Tugallanmagan deb sanaladigan holatlar — partial unique index filtri bilan bir xil.</summary>
        private static readonly CashCollectionStatus[] OpenStatuses =
            { CashCollectionStatus.Requested, CashCollectionStatus.BoxOpened };

        public CashCollectionRepository(AppDbContext context)
            => _context = context;

        public Task<CashCollectionEntity?> GetByIdAsync(long id)
            => _context.CashCollections
                .Include(c => c.Device)
                .FirstOrDefaultAsync(c => c.Id == id);

        public Task<CashCollectionEntity?> GetOpenByDeviceAsync(long deviceId)
            => _context.CashCollections
                .Where(c => c.DeviceId == deviceId && OpenStatuses.Contains(c.Status))
                .OrderByDescending(c => c.Id)
                .FirstOrDefaultAsync();

        public async Task<CashCollectionEntity> CreateAsync(CashCollectionEntity collection)
        {
            await _context.CashCollections.AddAsync(collection);
            await _context.SaveChangesAsync();
            return collection;
        }

        public async Task<CashCollectionEntity> UpdateAsync(CashCollectionEntity collection)
        {
            _context.CashCollections.Update(collection);
            await _context.SaveChangesAsync();
            return collection;
        }

        public Task<PagedResult<CashCollectionEntity>> GetAllAsync(
            PaginationParams param,
            long? merchantId = null,
            long? deviceId = null,
            long? incassatorUserId = null)
            => _context.CashCollections
                .Include(c => c.Device)
                .Where(c => merchantId == null || c.MerchantId == merchantId)
                .Where(c => deviceId == null || c.DeviceId == deviceId)
                .Where(c => incassatorUserId == null || c.IncassatorUserId == incassatorUserId)
                .ApplyListQuery(param)
                .ToPagedResultAsync(param);
    }
}
