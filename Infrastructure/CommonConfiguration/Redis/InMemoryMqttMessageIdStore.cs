using System.Collections.Concurrent;
using Domain.Interfaces;

namespace CommonConfiguration.Redis
{
    /// <summary>
    /// SessionApi process xotirasida (ConcurrentDictionary) yashovchi monotonic id tracker.
    /// Redis'ga yozmaydi — servis restart bo'lganda counter 0'dan boshlanadi, demak
    /// qurilma id=1 dan qayta yuborgan ham qabul qilinadi. Test va single-instance
    /// deploy uchun mos. Multi-instance kerak bo'lsa Redis variantiga qaytarish kerak.
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

        public Task ResetAsync(string serialNumber)
        {
            _inbound.TryRemove(serialNumber, out _);
            _outbound.TryRemove(serialNumber, out _);
            return Task.CompletedTask;
        }
    }
}
