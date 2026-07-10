using Domain.Interfaces;
using Microsoft.Extensions.Logging;

namespace CommonConfiguration.Redis
{
    /// <summary>
    /// Redis primary + in-memory shadow. Har bir muvaffaqiyatli amal in-memory'ga ham
    /// yoziladi — Redis vaqtincha yiqilsa monotonic tekshiruv xotiradagi oxirgi
    /// qiymatdan davom etadi (0'dan emas), Redis qaytganda esa undagi katta qiymat yana kuchga kiradi.
    /// </summary>
    public sealed class ResilientMqttMessageIdStore : IMqttMessageIdStore
    {
        private readonly RedisMqttMessageIdStore _primary;
        private readonly InMemoryMqttMessageIdStore _fallback;
        private readonly ILogger<ResilientMqttMessageIdStore> _logger;

        public ResilientMqttMessageIdStore(
            RedisMqttMessageIdStore primary,
            InMemoryMqttMessageIdStore fallback,
            ILogger<ResilientMqttMessageIdStore> logger)
        {
            _primary = primary;
            _fallback = fallback;
            _logger = logger;
        }

        public async Task<bool> TryAcceptInboundIdAsync(string serialNumber, long id)
        {
            try
            {
                var accepted = await _primary.TryAcceptInboundIdAsync(serialNumber, id);
                if (accepted)
                    await _fallback.TryAcceptInboundIdAsync(serialNumber, id); // shadow sync
                return accepted;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Redis TryAcceptInboundIdAsync failed, using in-memory fallback.");
                return await _fallback.TryAcceptInboundIdAsync(serialNumber, id);
            }
        }

        public async Task<long> NextOutboundIdAsync(string serialNumber)
        {
            try
            {
                var next = await _primary.NextOutboundIdAsync(serialNumber);
                await _fallback.SetOutboundFloorAsync(serialNumber, next); // shadow sync
                return next;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Redis NextOutboundIdAsync failed, using in-memory fallback.");
                return await _fallback.NextOutboundIdAsync(serialNumber);
            }
        }

        public async Task ResetAsync(string serialNumber)
        {
            try
            {
                await _primary.ResetAsync(serialNumber);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Redis ResetAsync failed, continuing with in-memory fallback.");
            }

            await _fallback.ResetAsync(serialNumber);
        }
    }
}
