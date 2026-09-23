using Domain.Interfaces.Telegram;
using StackExchange.Redis;

namespace CommonConfiguration.Redis
{
    /// <summary>
    /// Bir martalik "botga o'tish" tokenlari. Kalit: <c>tg_link:{token}</c> → userId.
    /// Iste'mol <c>GETDEL</c> bilan — atomik, ya'ni token faqat bir marta ishlaydi.
    /// </summary>
    public sealed class RedisTelegramLinkStore : ITelegramLinkStore
    {
        private const string KeyPrefix = "tg_link:";
        private readonly IConnectionMultiplexer _redis;

        public RedisTelegramLinkStore(IConnectionMultiplexer redis) => _redis = redis;

        public Task SetAsync(string token, long userId, TimeSpan ttl)
            => _redis.GetDatabase().StringSetAsync(KeyPrefix + token, userId, ttl);

        public async Task<long?> ConsumeAsync(string token)
        {
            var value = await _redis.GetDatabase().StringGetDeleteAsync(KeyPrefix + token);
            if (!value.HasValue) return null;
            return long.TryParse(value.ToString(), out var userId) ? userId : null;
        }
    }
}
