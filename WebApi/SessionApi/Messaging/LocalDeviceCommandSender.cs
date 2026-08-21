using Domain.Interfaces;
using Microsoft.Extensions.Logging;

namespace SessionApi.Messaging
{
    /// <summary>
    /// <see cref="IDeviceCommandSender"/> ning SessionApi implementatsiyasi.
    /// MQTT ulanishi shu process'da, shuning uchun ko'prik shart emas —
    /// buyruq to'g'ridan-to'g'ri publisher'ga beriladi.
    /// </summary>
    public sealed class LocalDeviceCommandSender : IDeviceCommandSender
    {
        private readonly IDeviceCommandPublisher _publisher;
        private readonly ILogger<LocalDeviceCommandSender> _logger;

        public LocalDeviceCommandSender(
            IDeviceCommandPublisher publisher,
            ILogger<LocalDeviceCommandSender> logger)
        {
            _publisher = publisher;
            _logger = logger;
        }

        public async Task<bool> SendCashBoxOpenAsync(
            string serialNumber, long collectionId, CancellationToken ct = default)
        {
            try
            {
                await _publisher.PublishCashBoxOpenAsync(serialNumber, collectionId, ct);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "[CMD] cash.box.open publish xatosi serial={Serial} collectionId={CollectionId}",
                    serialNumber, collectionId);
                return false;
            }
        }
    }
}
