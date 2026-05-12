using Domain.Interfaces;
using Microsoft.Extensions.Logging;

namespace CommonConfiguration.Redis
{
    /// <summary>
    /// Redis primary + in-memory fallback. Redis ishlamasa — app yiqilmaydi, lekin
    /// pending sessiya state node-local bo'lib qoladi. Multi-instance deploymentda bu
    /// holatda foydalanuvchi boshqa instansga tushsa pending topilmaydi va idempotent retry
    /// yangi token qaytaradi — qabul qilarli degradatsiya.
    /// </summary>
    public sealed class ResilientPendingSessionStore : IPendingSessionStore
    {
        private readonly RedisPendingSessionStore _primary;
        private readonly InMemoryPendingSessionStore _fallback;
        private readonly ILogger<ResilientPendingSessionStore> _logger;

        public ResilientPendingSessionStore(
            RedisPendingSessionStore primary,
            InMemoryPendingSessionStore fallback,
            ILogger<ResilientPendingSessionStore> logger)
        {
            _primary = primary;
            _fallback = fallback;
            _logger = logger;
        }

        public async Task SetAsync(long userId, string sessionToken, TimeSpan ttl)
        {
            try
            {
                await _primary.SetAsync(userId, sessionToken, ttl);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Redis SetAsync failed for pending session, using in-memory fallback.");
                await _fallback.SetAsync(userId, sessionToken, ttl);
            }
        }

        public async Task<PendingSessionEntry?> GetAsync(long userId)
        {
            try
            {
                var entry = await _primary.GetAsync(userId);
                if (entry is not null) return entry;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Redis GetAsync failed for pending session, using in-memory fallback.");
            }

            return await _fallback.GetAsync(userId);
        }

        public async Task DeleteAsync(long userId)
        {
            try
            {
                await _primary.DeleteAsync(userId);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Redis DeleteAsync failed for pending session, continuing with in-memory fallback.");
            }

            await _fallback.DeleteAsync(userId);
        }
    }
}
