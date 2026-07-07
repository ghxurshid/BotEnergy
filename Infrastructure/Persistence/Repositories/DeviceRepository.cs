using Domain.Dtos.Base;
using Domain.Dtos.Device;
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
                .Include(d => d.Station)
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

        public Task<PagedResult<DeviceEntity>> GetAllAsync(PaginationParams param, long? merchantId = null)
            => _context.Devices
                .Include(d => d.Station)
                .Where(d => merchantId == null || d.Station!.MerchantId == merchantId)
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
            if (_context.Entry(device).State == EntityState.Detached)
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

        public async Task<bool> MarkSeenAsync(string serialNumber)
        {
            var now = DateTime.Now;

            // 1) Edge: faqat oldin offline bo'lsa o'zgaradi (rows-affected == 1 ⇒ reconnect).
            var edge = await _context.Devices
                .Where(d => d.SerialNumber == serialNumber && d.IsActive && !d.IsOnline)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(d => d.IsOnline, true)
                    .SetProperty(d => d.LastSeenAt, now)
                    .SetProperty(d => d.UpdatedDate, now));

            if (edge > 0)
                return true;

            // 2) Allaqachon online — faqat LastSeenAt ni arzon yangilash (hot path).
            await _context.Devices
                .Where(d => d.SerialNumber == serialNumber && d.IsActive)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(d => d.LastSeenAt, now)
                    .SetProperty(d => d.UpdatedDate, now));

            return false;
        }

        public async Task<List<DeviceEntity>> GetStaleOnlineDevicesAsync(DateTime threshold)
        {
            return await _context.Devices
                .Include(d => d.Station)
                .Where(d => d.IsOnline && (d.LastSeenAt == null || d.LastSeenAt < threshold))
                .ToListAsync();
        }

        public Task<DeviceStatusInfo?> GetStatusInfoBySerialAsync(string serialNumber)
            => _context.Devices
                .Where(d => d.SerialNumber == serialNumber && d.IsActive)
                .Select(StatusProjection)
                .FirstOrDefaultAsync();

        public Task<DeviceStatusInfo?> GetStatusInfoByIdAsync(long deviceId)
            => _context.Devices
                .Where(d => d.Id == deviceId && d.IsActive)
                .Select(StatusProjection)
                .FirstOrDefaultAsync();

        public async Task<List<DeviceStatusInfo>> GetStatusInfoByMerchantAsync(long merchantId)
            => await _context.Devices
                .Where(d => d.IsActive && d.Station!.MerchantId == merchantId)
                .Select(StatusProjection)
                .ToListAsync();

        private static readonly System.Linq.Expressions.Expression<System.Func<DeviceEntity, DeviceStatusInfo>> StatusProjection =
            d => new DeviceStatusInfo(d.Id, d.SerialNumber, d.StationId, d.Station!.MerchantId, d.IsOnline, d.LastSeenAt);
    }
}
