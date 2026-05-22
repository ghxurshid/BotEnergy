namespace Domain.Interfaces
{
    /// <summary>
    /// Service qatlamidan qurilmaga buyruq yuborish abstraksiyasi.
    /// Implementatsiya MQTT publisher orqali to'g'ridan-to'g'ri broker'ga publish qiladi
    /// (oraliq RabbitMQ queue yo'q — Mobile→SessionApi va SessionApi→Qurilma bir process'da).
    /// </summary>
    public interface IDeviceCommandPublisher
    {
        Task PublishStartAsync(string serialNumber, long processId, long productId, decimal amount, string? productName = null, string? unit = null, decimal? pricePerUnit = null, CancellationToken ct = default);
        Task PublishStopAsync(string serialNumber, long processId, CancellationToken ct = default);
        Task PublishPauseAsync(string serialNumber, long processId, CancellationToken ct = default);
        Task PublishResumeAsync(string serialNumber, long processId, CancellationToken ct = default);
    }
}
