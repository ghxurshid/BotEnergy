using Domain.Interfaces.Telegram;
using Microsoft.Extensions.Logging;

namespace CommonConfiguration.Redis
{
    /// <summary>Redis asosiy, in-memory zaxira (pending sessiya bilan bir xil andoza).</summary>
    public sealed class ResilientTelegramLinkStore : ITelegramLinkStore
    {
        private readonly RedisTelegramLinkStore _primary;
        private readonly InMemoryTelegramLinkStore _fallback;
        private readonly ILogger<ResilientTelegramLinkStore> _logger;

        public ResilientTelegramLinkStore(
            RedisTelegramLinkStore primary,
            InMemoryTelegramLinkStore fallback,
            ILogger<ResilientTelegramLinkStore> logger)
        {
            _primary = primary;
            _fallback = fallback;
            _logger = logger;
        }

        public async Task SetAsync(string token, long userId, TimeSpan ttl)
        {
            try
            {
                await _primary.SetAsync(token, userId, ttl);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[TG] Redis SetAsync ishlamadi — zaxiraga yozildi.");
                await _fallback.SetAsync(token, userId, ttl);
            }
        }

        public async Task<long?> ConsumeAsync(string token)
        {
            try
            {
                var userId = await _primary.ConsumeAsync(token);
                if (userId is not null) return userId;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[TG] Redis ConsumeAsync ishlamadi — zaxira o'qildi.");
            }

            return await _fallback.ConsumeAsync(token);
        }
    }
}
