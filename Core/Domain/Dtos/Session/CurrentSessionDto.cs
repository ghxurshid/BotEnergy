namespace Domain.Dtos.Session
{
    /// <summary>
    /// Foydalanuvchining hozirgi aktiv sessiyasi haqida snapshot.
    /// Resume va Bootstrap flow lar uchun ishlatiladi.
    /// </summary>
    public class CurrentSessionDto
    {
        public long SessionId { get; set; }
        public string SessionToken { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public DateTime? ConnectedAt { get; set; }
        public DateTime LastActivityAt { get; set; }
        public DateTime IdleAfter { get; set; }

        public CurrentSessionDeviceDto? Device { get; set; }
        public CurrentSessionProcessDto? ActiveProcess { get; set; }
    }

    public class CurrentSessionDeviceDto
    {
        public long DeviceId { get; set; }
        public string SerialNumber { get; set; } = string.Empty;
        public string DeviceType { get; set; } = string.Empty;
        public bool IsOnline { get; set; }
        public DateTime? LastSeenAt { get; set; }
    }

    public class CurrentSessionProcessDto
    {
        public long ProcessId { get; set; }
        public long ProductId { get; set; }
        public string ProductName { get; set; } = string.Empty;
        public string Unit { get; set; } = string.Empty;
        public decimal PricePerUnit { get; set; }
        public decimal RequestedAmount { get; set; }
        public decimal GivenAmount { get; set; }
        public decimal CurrentCost { get; set; }
        public string Status { get; set; } = string.Empty;
        public DateTime StartedAt { get; set; }
        public DateTime? PausedAt { get; set; }
    }
}
