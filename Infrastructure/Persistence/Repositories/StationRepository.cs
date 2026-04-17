using Domain.Entities;
using Domain.Repositories;
using Microsoft.EntityFrameworkCore;
using Persistence.Context;

namespace Persistence.Repositories
{
    public class StationRepository : IStationRepository
    {
        private readonly AppDbContext _context;

        public StationRepository(AppDbContext context)
            => _context = context;

        public async Task<StationEntity?> GetByIdAsync(long id)
            => await _context.Stations
                .Include(s => s.Organization)
                .FirstOrDefaultAsync(s => s.Id == id);

        public async Task<List<StationEntity>> GetAllAsync()
            => await _context.Stations
                .Include(s => s.Organization)
                .OrderBy(s => s.Name)
                .ToListAsync();

        public async Task<List<StationEntity>> GetByOrganizationIdAsync(long organizationId)
            => await _context.Stations
                .Include(s => s.Organization)
                .Where(s => s.OrganizationId == organizationId)
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
