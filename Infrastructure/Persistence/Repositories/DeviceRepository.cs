using Domain.Dtos.Base;
using Domain.Entities;
using Domain.Repositories;
using Microsoft.EntityFrameworkCore;
using Persistence.Context;
using Persistence.Extensions;

namespace Persistence.Repositories
{
    public class DeviceRepository : IDeviceRepository
    {
        private readonly AppDbContext _context;

        public DeviceRepository(AppDbContext context)
            => _context = context;

        public async Task<DeviceEntity?> GetByIdAsync(long id)
        {
            return await _context.Devices
                .FirstOrDefaultAsync(d => d.Id == id && d.IsActive);
        }

        public async Task<DeviceEntity?> GetBySerialNumberAsync(string serialNumber)
        {
            return await _context.Devices
                .Include(d => d.Station)
                .Include(d => d.Products!.Where(p => p.IsActive))
                .FirstOrDefaultAsync(d => d.SerialNumber == serialNumber && d.IsActive);
        }

        public async Task<bool> ValidateDeviceAsync(string serialNumber, string secretKey)
        {
            return await _context.Devices
                .AnyAsync(d => d.SerialNumber == serialNumber
                            && d.SecretKey == secretKey
                            && d.IsActive);
        }

        public Task<PagedResult<DeviceEntity>> GetAllAsync(PaginationParams param)
            => _context.Devices
                .Include(d => d.Station)
                .OrderBy(d => d.SerialNumber)
                .ToPagedResultAsync(param);

        public async Task<List<DeviceEntity>> GetByStationIdAsync(long stationId)
            => await _context.Devices
                .Include(d => d.Station)
                .Where(d => d.StationId == stationId)
                .OrderBy(d => d.SerialNumber)
                .ToListAsync();

        public async Task<DeviceEntity> CreateAsync(DeviceEntity device)
        {
            await _context.Devices.AddAsync(device);
            await _context.SaveChangesAsync();
            return device;
        }

        public async Task<DeviceEntity> UpdateAsync(DeviceEntity device)
        {
            _context.Devices.Update(device);
            await _context.SaveChangesAsync();
            return device;
        }

        public async Task DeleteAsync(long id)
        {
            var device = await _context.Devices.FirstOrDefaultAsync(d => d.Id == id);
            if (device is null) return;
            device.IsDeleted = true;
            device.IsActive = false;
            await _context.SaveChangesAsync();
        }
    }
}
