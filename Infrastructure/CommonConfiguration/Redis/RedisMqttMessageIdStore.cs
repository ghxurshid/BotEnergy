using Domain.Interfaces;
using StackExchange.Redis;

namespace CommonConfiguration.Redis
{
    /// <summary>
    /// Redis'da yashovchi monotonic id tracker. Counter'lar TTL'siz saqlanadi —
    /// servis restart bo'lsa ham qiymatlar yo'qolmaydi (Redis persistence).
    /// Reset faqat maxsus expert-rejim endpoint'i orqali (qurilma EEPROM qayta
    /// flash qilinganda) chaqiriladi — oddiy biznes oqimida hech qachon.
    /// </summary>
    public sealed class RedisMqttMessageIdStore : IMqttMessageIdStore
    {
        private const string InboundKeyPrefix = "mqttid:in:";
        private const string OutboundKeyPrefix = "mqttid:out:";

        // Atomik "id > current bo'lsa yoz" — race'siz monotonic accept.
        private const string AcceptScript = @"
local current = tonumber(redis.call('GET', KEYS[1]) or '0')
local id = tonumber(ARGV[1])
if id > current then
    redis.call('SET', KEYS[1], ARGV[1])
    return 1
end
return 0";

        private readonly IConnectionMultiplexer _redis;

        public RedisMqttMessageIdStore(IConnectionMultiplexer redis)
            => _redis = redis;

        public async Task<bool> TryAcceptInboundIdAsync(string serialNumber, long id)
        {
            var result = await _redis.GetDatabase().ScriptEvaluateAsync(
                AcceptScript,
                new RedisKey[] { InboundKeyPrefix + serialNumber },
                new RedisValue[] { id });
            return (long)result == 1;
        }

        public Task<long> NextOutboundIdAsync(string serialNumber)
            => _redis.GetDatabase().StringIncrementAsync(OutboundKeyPrefix + serialNumber);

        public async Task ResetAsync(string serialNumber)
        {
            var db = _redis.GetDatabase();
            await db.KeyDeleteAsync(new RedisKey[]
            {
                InboundKeyPrefix + serialNumber,
                OutboundKeyPrefix + serialNumber
            });
        }
    }
}
