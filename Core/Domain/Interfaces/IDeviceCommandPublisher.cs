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

        /// <summary>
        /// Sessiya yopilgani haqida qurilmani xabardor qiladi — QR/ekranni tozalab,
        /// idle holatga qaytishi uchun. Jarayon allaqachon tugagan bo'lsa ham yuboriladi
        /// (process-level stop bilan emas, sessiya darajasida).
        /// </summary>
        Task PublishSessionClosedAsync(string serialNumber, long sessionId, string reason, CancellationToken ct = default);

        /// <summary>
        /// Sessiya hold balansi o'zgardi — qurilma displeyida ko'rsatish uchun.
        /// SignalR SessionBalanceChanged bilan BIR XIL event modeli (MQTT type: balance.update).
        /// </summary>
        Task PublishBalanceUpdateAsync(string serialNumber, Domain.Dtos.PaymentSession.SessionBalanceChangedDto e, CancellationToken ct = default);
    }
}
