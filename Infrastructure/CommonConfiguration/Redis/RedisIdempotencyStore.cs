using System.Text.Json;
using Domain.Interfaces;
using StackExchange.Redis;

namespace CommonConfiguration.Redis
{
    public sealed class RedisIdempotencyStore : IIdempotencyStore
    {
        private const string KeyPrefix = "idem:";
        private const string ReservedMarker = "__reserved__";

        private readonly IConnectionMultiplexer _redis;

        public RedisIdempotencyStore(IConnectionMultiplexer redis)
        {
            _redis = redis;
        }

        public async Task<IdempotencyEntry?> TryGetAsync(string key)
        {
            var value = await _redis.GetDatabase().StringGetAsync(KeyPrefix + key);
            if (!value.HasValue)
                return null;

            var raw = value.ToString();
            if (raw == ReservedMarker)
                return null;

            try
            {
                return JsonSerializer.Deserialize<IdempotencyEntry>(raw);
            }
            catch
            {
                return null;
            }
        }

        public Task<bool> TryReserveAsync(string key, TimeSpan reservation)
        {
            return _redis.GetDatabase().StringSetAsync(
                KeyPrefix + key, ReservedMarker, reservation, When.NotExists);
        }

        public Task SaveResponseAsync(string key, int statusCode, string body, TimeSpan ttl)
        {
            var json = JsonSerializer.Serialize(new IdempotencyEntry
            {
                StatusCode = statusCode,
                Body = body
            });
            return _redis.GetDatabase().StringSetAsync(KeyPrefix + key, json, ttl);
        }

        public Task ReleaseAsync(string key)
        {
            return _redis.GetDatabase().KeyDeleteAsync(KeyPrefix + key);
        }
    }
}
