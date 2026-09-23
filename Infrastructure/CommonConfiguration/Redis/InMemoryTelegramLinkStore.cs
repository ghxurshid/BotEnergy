using System.Collections.Concurrent;
using Domain.Interfaces.Telegram;

namespace CommonConfiguration.Redis
{
    /// <summary>Redis ishlamaganda ishlatiladigan node-local zaxira.</summary>
    public sealed class InMemoryTelegramLinkStore : ITelegramLinkStore
    {
        private readonly ConcurrentDictionary<string, (long UserId, DateTime ExpiresAt)> _items = new();

        public Task SetAsync(string token, long userId, TimeSpan ttl)
        {
            _items[token] = (userId, DateTime.Now.Add(ttl));
            return Task.CompletedTask;
        }

        public Task<long?> ConsumeAsync(string token)
        {
            if (!_items.TryRemove(token, out var entry))
                return Task.FromResult<long?>(null);

            return Task.FromResult<long?>(entry.ExpiresAt > DateTime.Now ? entry.UserId : null);
        }
    }
}
