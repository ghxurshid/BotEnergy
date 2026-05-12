using System.Collections.Concurrent;
using Domain.Interfaces;

namespace CommonConfiguration.Redis
{
    /// <summary>
    /// Redis mavjud emas paytda ishlatiluvchi fallback. Process xotirasida saqlanadi,
    /// app restart bilan yo'qoladi — bu pending sessiya uchun maqbul (qisqa TTL).
    /// </summary>
    public sealed class InMemoryPendingSessionStore : IPendingSessionStore
    {
        private readonly ConcurrentDictionary<long, PendingSessionEntry> _store = new();

        public Task SetAsync(long userId, string sessionToken, TimeSpan ttl)
        {
            _store[userId] = new PendingSessionEntry(sessionToken, DateTime.Now.Add(ttl));
            return Task.CompletedTask;
        }

        public Task<PendingSessionEntry?> GetAsync(long userId)
        {
            if (_store.TryGetValue(userId, out var entry))
            {
                if (entry.ExpiresAt > DateTime.Now)
                    return Task.FromResult<PendingSessionEntry?>(entry);

                _store.TryRemove(userId, out _);
            }

            return Task.FromResult<PendingSessionEntry?>(null);
        }

        public Task DeleteAsync(long userId)
        {
            _store.TryRemove(userId, out _);
            return Task.CompletedTask;
        }
    }
}
