using Domain.Entities;
using Domain.Repositories;
using Microsoft.EntityFrameworkCore;
using Persistence.Context;

namespace Persistence.Repositories
{
    public class DeviceRepository : IDeviceRepository
    {
        private readonly AppDbContext _context;

        public DeviceRepository(AppDbContext context)
            => _context = context;

        public async Task<DeviceEntity?> GetBySerialNumberAsync(string serialNumber)
        {
            return await _context.Devices
                .Include(d => d.Station)
                .FirstOrDefaultAsync(d => d.SerialNumber == serialNumber && d.IsActive && !d.IsDeleted);
        }
    }
}
