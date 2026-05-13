using Domain.Interfaces;
using StackExchange.Redis;

namespace CommonConfiguration.Redis
{
    /// <summary>
    /// Redis backed id store. Kalitlar:
    /// <list type="bullet">
    /// <item><c>mqtt_d2s_id:{serial}</c> — qurilmadan kelgan eng katta id (inbound).</item>
    /// <item><c>mqtt_s2d_id:{serial}</c> — serverdan jo'natilgan eng katta id (outbound counter).</item>
    /// </list>
    /// TTL yo'q — qurilma deactivate qilinmaguncha counter saqlanadi (qurilma reset bo'lsa firmware
    /// boshidan 1 dan boshlanmasligi kerak — boot paytida flash'dan o'qib oladi).
    /// </summary>
    public sealed class RedisMqttMessageIdStore : IMqttMessageIdStore
    {
        private readonly IConnectionMultiplexer _redis;
        private const string InboundPrefix = "mqtt_d2s_id:";
        private const string OutboundPrefix = "mqtt_s2d_id:";

        public RedisMqttMessageIdStore(IConnectionMultiplexer redis)
        {
            _redis = redis;
        }

        public async Task<bool> TryAcceptInboundIdAsync(string serialNumber, long id)
        {
            var db = _redis.GetDatabase();
            var key = InboundPrefix + serialNumber;

            // Lua script: read current, accept only if id > current, atomically update.
            // KEYS[1] = key, ARGV[1] = new id
            const string script = @"
                local cur = tonumber(redis.call('GET', KEYS[1]) or '0')
                local new = tonumber(ARGV[1])
                if new > cur then
                    redis.call('SET', KEYS[1], new)
                    return 1
                else
                    return 0
                end";

            var result = (long)await db.ScriptEvaluateAsync(script,
                new RedisKey[] { key },
                new RedisValue[] { id });

            return result == 1;
        }

        public async Task<long> NextOutboundIdAsync(string serialNumber)
        {
            var db = _redis.GetDatabase();
            return await db.StringIncrementAsync(OutboundPrefix + serialNumber);
        }
    }
}
