namespace Domain.Interfaces
{
    /// <summary>
    /// Service qatlamidan qurilmaga buyruq yuborish abstraksiyasi.
    /// Implementatsiya RabbitMQ → DeviceApi → MQTT zanjirini ishga tushiradi.
    /// </summary>
    public interface IDeviceCommandPublisher
    {
        void PublishStart(string serialNumber, long processId, long productId, decimal amount, string? productName = null, string? unit = null, decimal? pricePerUnit = null);
        void PublishStop(string serialNumber, long processId);
        void PublishPause(string serialNumber, long processId);
        void PublishResume(string serialNumber, long processId);
    }
}
