using Domain.Interfaces;
using Microsoft.Extensions.Logging;

namespace CommonConfiguration.Redis
{
    /// <summary>
    /// Redis asosiy, in-memory zaxira. Redis yiqilsa QR oqimi to'xtab qolmaydi
    /// (<see cref="ResilientPendingSessionStore"/> bilan bir xil andoza).
    /// </summary>
    public sealed class ResilientDeviceQrStore : IDeviceQrStore
    {
        private readonly RedisDeviceQrStore _primary;
        private readonly InMemoryDeviceQrStore _fallback;
        private readonly ILogger<ResilientDeviceQrStore> _logger;

        public ResilientDeviceQrStore(
            RedisDeviceQrStore primary,
            InMemoryDeviceQrStore fallback,
            ILogger<ResilientDeviceQrStore> logger)
        {
            _primary = primary;
            _fallback = fallback;
            _logger = logger;
        }

        public async Task SetAsync(string code, DeviceQrEntry entry, TimeSpan ttl)
        {
            try
            {
                await _primary.SetAsync(code, entry, ttl);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[QR] Redis SetAsync ishlamadi — in-memory zaxiraga yozildi.");
                await _fallback.SetAsync(code, entry, ttl);
            }
        }

        public async Task<DeviceQrEntry?> GetAsync(string code)
        {
            try
            {
                var entry = await _primary.GetAsync(code);
                if (entry is not null) return entry;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[QR] Redis GetAsync ishlamadi — zaxira o'qildi.");
            }

            return await _fallback.GetAsync(code);
        }

        public async Task<DeviceQrEntry?> ConsumeAsync(string code)
        {
            try
            {
                var entry = await _primary.ConsumeAsync(code);
                if (entry is not null) return entry;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[QR] Redis ConsumeAsync ishlamadi — zaxira ishlatildi.");
            }

            return await _fallback.ConsumeAsync(code);
        }

        public async Task InvalidateForDeviceAsync(long deviceId)
        {
            try
            {
                await _primary.InvalidateForDeviceAsync(deviceId);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[QR] Redis InvalidateForDeviceAsync ishlamadi.");
            }

            await _fallback.InvalidateForDeviceAsync(deviceId);
        }
    }
}
