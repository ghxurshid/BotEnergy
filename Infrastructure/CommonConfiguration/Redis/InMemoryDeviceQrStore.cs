using System.Collections.Concurrent;
using Domain.Interfaces;

namespace CommonConfiguration.Redis
{
    /// <summary>
    /// Redis ishlamay qolganda ishlatiladigan zaxira (node-local) ombor.
    /// Ko'p instansli deploymentda kod boshqa instansga tushsa topilmaydi —
    /// bu holda qurilma yangi kod so'raydi, ya'ni degradatsiya qabul qilarli.
    /// </summary>
    public sealed class InMemoryDeviceQrStore : IDeviceQrStore
    {
        private readonly ConcurrentDictionary<string, DeviceQrEntry> _byCode = new();
        private readonly ConcurrentDictionary<long, string> _byDevice = new();

        public Task SetAsync(string code, DeviceQrEntry entry, TimeSpan ttl)
        {
            if (_byDevice.TryGetValue(entry.DeviceId, out var previous))
                _byCode.TryRemove(previous, out _);

            _byCode[code] = entry with { ExpiresAt = DateTime.Now.Add(ttl) };
            _byDevice[entry.DeviceId] = code;
            return Task.CompletedTask;
        }

        public Task<DeviceQrEntry?> GetAsync(string code)
        {
            if (!_byCode.TryGetValue(code, out var entry))
                return Task.FromResult<DeviceQrEntry?>(null);

            if (entry.ExpiresAt <= DateTime.Now)
            {
                Remove(code, entry.DeviceId);
                return Task.FromResult<DeviceQrEntry?>(null);
            }

            return Task.FromResult<DeviceQrEntry?>(entry);
        }

        public Task<DeviceQrEntry?> ConsumeAsync(string code)
        {
            if (!_byCode.TryRemove(code, out var entry))
                return Task.FromResult<DeviceQrEntry?>(null);

            _byDevice.TryRemove(entry.DeviceId, out _);

            return Task.FromResult<DeviceQrEntry?>(entry.ExpiresAt > DateTime.Now ? entry : null);
        }

        public Task InvalidateForDeviceAsync(long deviceId)
        {
            if (_byDevice.TryRemove(deviceId, out var code))
                _byCode.TryRemove(code, out _);

            return Task.CompletedTask;
        }

        private void Remove(string code, long deviceId)
        {
            _byCode.TryRemove(code, out _);
            _byDevice.TryRemove(deviceId, out _);
        }
    }
}
