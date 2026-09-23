using System.Text.Json;
using Domain.Interfaces;
using StackExchange.Redis;

namespace CommonConfiguration.Redis
{
    /// <summary>
    /// Redis backed bir martalik QR kodlar ombori.
    ///
    /// Kalitlar:
    ///   <c>device_qr:code:{code}</c>     → {deviceId, serial} (TTL bilan)
    ///   <c>device_qr:device:{deviceId}</c> → joriy kod (eskisini bekor qilish uchun)
    ///
    /// Iste'mol qilish <c>GETDEL</c> bilan — atomik, ya'ni bitta kodni ikki mijoz
    /// bir vaqtda ishlatolmaydi.
    /// </summary>
    public sealed class RedisDeviceQrStore : IDeviceQrStore
    {
        private readonly IConnectionMultiplexer _redis;

        private const string CodeKeyPrefix = "device_qr:code:";
        private const string DeviceKeyPrefix = "device_qr:device:";

        public RedisDeviceQrStore(IConnectionMultiplexer redis) => _redis = redis;

        public async Task SetAsync(string code, DeviceQrEntry entry, TimeSpan ttl)
        {
            var db = _redis.GetDatabase();

            // Bitta qurilmada bir vaqtda bitta amaldagi kod bo'lsin.
            var previous = await db.StringGetAsync(DeviceKey(entry.DeviceId));
            if (previous.HasValue)
                await db.KeyDeleteAsync(CodeKey(previous.ToString()));

            var payload = JsonSerializer.Serialize(new StoredEntry(entry.DeviceId, entry.SerialNumber));

            await db.StringSetAsync(CodeKey(code), payload, ttl);
            await db.StringSetAsync(DeviceKey(entry.DeviceId), code, ttl);
        }

        public async Task<DeviceQrEntry?> GetAsync(string code)
        {
            var db = _redis.GetDatabase();
            var key = CodeKey(code);

            var value = await db.StringGetAsync(key);
            if (!value.HasValue) return null;

            var ttl = await db.KeyTimeToLiveAsync(key);
            return Map(value.ToString(), ttl);
        }

        public async Task<DeviceQrEntry?> ConsumeAsync(string code)
        {
            var db = _redis.GetDatabase();
            var key = CodeKey(code);

            var ttl = await db.KeyTimeToLiveAsync(key);
            var value = await db.StringGetDeleteAsync(key);
            if (!value.HasValue) return null;

            var entry = Map(value.ToString(), ttl);
            if (entry is not null)
                await db.KeyDeleteAsync(DeviceKey(entry.DeviceId));

            return entry;
        }

        public async Task InvalidateForDeviceAsync(long deviceId)
        {
            var db = _redis.GetDatabase();
            var code = await db.StringGetAsync(DeviceKey(deviceId));
            if (code.HasValue)
                await db.KeyDeleteAsync(CodeKey(code.ToString()));

            await db.KeyDeleteAsync(DeviceKey(deviceId));
        }

        private static DeviceQrEntry? Map(string json, TimeSpan? ttl)
        {
            var stored = JsonSerializer.Deserialize<StoredEntry>(json);
            if (stored is null) return null;

            var expiresAt = ttl.HasValue ? DateTime.Now.Add(ttl.Value) : DateTime.Now;
            return new DeviceQrEntry(stored.DeviceId, stored.SerialNumber, expiresAt);
        }

        private static string CodeKey(string code) => CodeKeyPrefix + code;
        private static string DeviceKey(long deviceId) => DeviceKeyPrefix + deviceId;

        private sealed record StoredEntry(long DeviceId, string SerialNumber);
    }
}
