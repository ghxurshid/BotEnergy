using Domain.Dtos.Base;
using Domain.Entities;
using Domain.Repositories;
using Microsoft.EntityFrameworkCore;
using Persistence.Context;
using Persistence.Extensions;

namespace Persistence.Repositories
{
    public class StationRepository : IStationRepository
    {
        private readonly AppDbContext _context;

        public StationRepository(AppDbContext context)
            => _context = context;

        public async Task<StationEntity?> GetByIdAsync(long id)
            => await _context.Stations
                .Include(s => s.Merchant)
                .FirstOrDefaultAsync(s => s.Id == id);

        public Task<PagedResult<StationEntity>> GetAllAsync(PaginationParams param)
            => _context.Stations
                .Include(s => s.Merchant)
                .OrderBy(s => s.Name)
                .ToPagedResultAsync(param);

        public async Task<List<StationEntity>> GetByMerchantIdAsync(long merchantId)
            => await _context.Stations
                .Include(s => s.Merchant)
                .Where(s => s.MerchantId == merchantId)
                .OrderBy(s => s.Name)
                .ToListAsync();

        public async Task<StationEntity> CreateAsync(StationEntity station)
        {
            await _context.Stations.AddAsync(station);
            await _context.SaveChangesAsync();
            return station;
        }

        public async Task<StationEntity> UpdateAsync(StationEntity station)
        {
            _context.Stations.Update(station);
            await _context.SaveChangesAsync();
            return station;
        }

        public async Task DeleteAsync(long id)
        {
            var station = await _context.Stations.FirstOrDefaultAsync(s => s.Id == id);
            if (station is null) return;
            station.IsDeleted = true;
            station.IsActive = false;
            await _context.SaveChangesAsync();
        }
    }
}
