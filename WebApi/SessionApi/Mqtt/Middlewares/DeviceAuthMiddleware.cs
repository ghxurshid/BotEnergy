using Domain.Repositories;
using Microsoft.Extensions.Logging;
using SessionApi.Mqtt.Abstractions;

namespace SessionApi.Mqtt.Middlewares
{
    /// <summary>
    /// Serial bo'yicha device'ni DB'dan topadi va <see cref="MqttContext.Device"/>'ga qo'yadi.
    /// Device topilmasa — pipeline to'xtaydi (auth o'tmagan).
    /// </summary>
    public sealed class DeviceAuthMiddleware : IMqttMiddleware
    {
        private readonly IDeviceRepository _deviceRepository;
        private readonly ILogger<DeviceAuthMiddleware> _logger;

        public DeviceAuthMiddleware(IDeviceRepository deviceRepository, ILogger<DeviceAuthMiddleware> logger)
        {
            _deviceRepository = deviceRepository;
            _logger = logger;
        }

        public async Task InvokeAsync(MqttContext context, MqttNext next)
        {
            var device = await _deviceRepository.GetBySerialNumberAsync(context.SerialNumber);
            if (device is null)
            {
                _logger.LogWarning(
                    "[MQTT-IN] Device topilmadi serial={Serial} topic={Topic}",
                    context.SerialNumber, context.Topic);
                return;
            }

            context.Device = device;
            await next();
        }
    }
}
