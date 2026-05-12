using Domain.Interfaces;
using StackExchange.Redis;

namespace CommonConfiguration.Redis
{
    /// <summary>
    /// Redis backed pending sessiya store. Key: pending_session:user:{userId} → sessionToken.
    /// TTL Redis tomonidan boshqariladi — kalit avtomatik o'chadi.
    /// </summary>
    public sealed class RedisPendingSessionStore : IPendingSessionStore
    {
        private readonly IConnectionMultiplexer _redis;
        private const string KeyPrefix = "pending_session:user:";

        public RedisPendingSessionStore(IConnectionMultiplexer redis)
        {
            _redis = redis;
        }

        public async Task SetAsync(long userId, string sessionToken, TimeSpan ttl)
        {
            var db = _redis.GetDatabase();
            await db.StringSetAsync(Key(userId), sessionToken, ttl);
        }

        public async Task<PendingSessionEntry?> GetAsync(long userId)
        {
            var db = _redis.GetDatabase();
            var key = Key(userId);

            var value = await db.StringGetAsync(key);
            if (!value.HasValue)
                return null;

            var ttl = await db.KeyTimeToLiveAsync(key);
            var expiresAt = ttl.HasValue ? DateTime.Now.Add(ttl.Value) : DateTime.Now;

            return new PendingSessionEntry(value.ToString(), expiresAt);
        }

        public async Task DeleteAsync(long userId)
        {
            var db = _redis.GetDatabase();
            await db.KeyDeleteAsync(Key(userId));
        }

        private static string Key(long userId) => KeyPrefix + userId.ToString();
    }
}
