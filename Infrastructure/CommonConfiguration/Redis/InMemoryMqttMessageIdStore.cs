using System.Collections.Concurrent;
using Domain.Interfaces;

namespace CommonConfiguration.Redis
{
    /// <summary>
    /// Process xotirasida (ConcurrentDictionary) yashovchi monotonic id tracker.
    /// Yakka o'zi ishlatilmaydi — <see cref="ResilientMqttMessageIdStore"/> ichida
    /// Redis'ning shadow/fallback nusxasi sifatida xizmat qiladi: Redis yiqilganda
    /// monotonic tekshiruv xotiradagi oxirgi ko'rilgan qiymatdan davom etadi.
    /// </summary>
    public sealed class InMemoryMqttMessageIdStore : IMqttMessageIdStore
    {
        private readonly ConcurrentDictionary<string, long> _inbound = new();
        private readonly ConcurrentDictionary<string, long> _outbound = new();

        public Task<bool> TryAcceptInboundIdAsync(string serialNumber, long id)
        {
            while (true)
            {
                var current = _inbound.GetOrAdd(serialNumber, 0L);
                if (id <= current)
                    return Task.FromResult(false);

                if (_inbound.TryUpdate(serialNumber, id, current))
                    return Task.FromResult(true);
                // Boshqa thread o'zgartirgan — qayta urinib ko'ramiz.
            }
        }

        public Task<long> NextOutboundIdAsync(string serialNumber)
        {
            var next = _outbound.AddOrUpdate(serialNumber, 1L, (_, v) => v + 1);
            return Task.FromResult(next);
        }

        /// <summary>
        /// Outbound counter'ni kamida <paramref name="value"/> ga ko'taradi (shadow sync uchun).
        /// Redis'dan olingan qiymatdan kichik bo'lsa fallback davomiyligi buzilmasin deb chaqiriladi.
        /// </summary>
        public Task SetOutboundFloorAsync(string serialNumber, long value)
        {
            _outbound.AddOrUpdate(serialNumber, value, (_, v) => Math.Max(v, value));
            return Task.CompletedTask;
        }

        public Task ResetAsync(string serialNumber)
        {
            _inbound.TryRemove(serialNumber, out _);
            _outbound.TryRemove(serialNumber, out _);
            return Task.CompletedTask;
        }
    }
}
