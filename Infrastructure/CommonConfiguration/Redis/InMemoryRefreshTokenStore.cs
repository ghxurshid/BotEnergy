using Domain.Interfaces;
using System.Collections.Concurrent;

namespace CommonConfiguration.Redis
{
    public sealed class InMemoryRefreshTokenStore : IRefreshTokenStore
    {
        private sealed record Entry(long UserId, DateTimeOffset ExpiresAt);

        private readonly ConcurrentDictionary<string, Entry> _store = new();

        public Task SaveAsync(string token, long userId, TimeSpan expiry)
        {
            _store[token] = new Entry(userId, DateTimeOffset.UtcNow.Add(expiry));
            return Task.CompletedTask;
        }

        public Task<long?> GetUserIdAsync(string token)
        {
            if (_store.TryGetValue(token, out var entry))
            {
                if (entry.ExpiresAt > DateTimeOffset.UtcNow)
                    return Task.FromResult<long?>(entry.UserId);

                _store.TryRemove(token, out _);
            }

            return Task.FromResult<long?>(null);
        }

        public Task RevokeAsync(string token)
        {
            _store.TryRemove(token, out _);
            return Task.CompletedTask;
        }
    }
}
