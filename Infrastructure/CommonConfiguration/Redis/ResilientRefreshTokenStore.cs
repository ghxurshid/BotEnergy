using Domain.Interfaces;
using Microsoft.Extensions.Logging;

namespace CommonConfiguration.Redis
{
    public sealed class ResilientRefreshTokenStore : IRefreshTokenStore
    {
        private readonly RedisRefreshTokenStore _primary;
        private readonly InMemoryRefreshTokenStore _fallback;
        private readonly ILogger<ResilientRefreshTokenStore> _logger;

        public ResilientRefreshTokenStore(
            RedisRefreshTokenStore primary,
            InMemoryRefreshTokenStore fallback,
            ILogger<ResilientRefreshTokenStore> logger)
        {
            _primary = primary;
            _fallback = fallback;
            _logger = logger;
        }

        public async Task SaveAsync(string token, long userId, TimeSpan expiry)
        {
            try
            {
                await _primary.SaveAsync(token, userId, expiry);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Redis SaveAsync failed, using in-memory fallback.");
                await _fallback.SaveAsync(token, userId, expiry);
            }
        }

        public async Task<long?> GetUserIdAsync(string token)
        {
            try
            {
                var id = await _primary.GetUserIdAsync(token);
                if (id.HasValue) return id;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Redis GetUserIdAsync failed, using in-memory fallback.");
            }

            return await _fallback.GetUserIdAsync(token);
        }

        public async Task RevokeAsync(string token)
        {
            try
            {
                await _primary.RevokeAsync(token);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Redis RevokeAsync failed, continuing with in-memory fallback.");
            }

            await _fallback.RevokeAsync(token);
        }
    }
}
