using Domain.Interfaces;
using StackExchange.Redis;

namespace CommonConfiguration.Redis
{
    public sealed class RedisRefreshTokenStore : IRefreshTokenStore
    {
        private readonly IConnectionMultiplexer _redis;
        private const string KeyPrefix = "refresh:";

        public RedisRefreshTokenStore(IConnectionMultiplexer redis)
        {
            _redis = redis;
        }

        public async Task SaveAsync(string token, long userId, TimeSpan expiry)
        {
            var db = _redis.GetDatabase();
            await db.StringSetAsync(KeyPrefix + token, userId.ToString(), expiry);
        }

        public async Task<long?> GetUserIdAsync(string token)
        {
            var db = _redis.GetDatabase();
            var value = await db.StringGetAsync(KeyPrefix + token);

            if (!value.HasValue)
                return null;

            return long.TryParse(value, out var userId) ? userId : null;
        }

        public async Task RevokeAsync(string token)
        {
            var db = _redis.GetDatabase();
            await db.KeyDeleteAsync(KeyPrefix + token);
        }
    }
}
