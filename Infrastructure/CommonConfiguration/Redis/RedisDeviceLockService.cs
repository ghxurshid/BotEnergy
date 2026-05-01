using StackExchange.Redis;
using Domain.Interfaces;

namespace CommonConfiguration.Redis
{
    public sealed class RedisDeviceLockService : IDeviceLockService
    {
        private readonly IConnectionMultiplexer _redis;
        private static readonly TimeSpan DefaultExpiry = TimeSpan.FromMinutes(30);
        private const string KeyPrefix = "device:lock:";

        public RedisDeviceLockService(IConnectionMultiplexer redis)
        {
            _redis = redis;
        }

        public async Task<bool> TryLockDeviceAsync(string serialNumber, long userId, TimeSpan? expiry = null)
        {
            var db = _redis.GetDatabase();
            var key = KeyPrefix + serialNumber;
            var ttl = expiry ?? DefaultExpiry;

            // SET NX — faqat kalit yo'q bo'lganda yozadi (atomic)
            return await db.StringSetAsync(key, userId.ToString(), ttl, When.NotExists);
        }

        public async Task<bool> UnlockDeviceAsync(string serialNumber, long userId)
        {
            var db = _redis.GetDatabase();
            var key = KeyPrefix + serialNumber;

            var currentOwner = await db.StringGetAsync(key);
            if (!currentOwner.HasValue || currentOwner != userId.ToString())
                return false;

            return await db.KeyDeleteAsync(key);
        }

        public async Task<long?> GetLockOwnerAsync(string serialNumber)
        {
            var db = _redis.GetDatabase();
            var key = KeyPrefix + serialNumber;

            var value = await db.StringGetAsync(key);
            if (!value.HasValue)
                return null;

            return long.TryParse(value, out var userId) ? userId : null;
        }
    }
}
