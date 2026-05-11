namespace Domain.Dtos.Report
{
    /// <summary>
    /// Bitta hisobot satri — bitta tugagan/aktiv jarayon ma'lumotlari.
    /// Snapshot maydonlar ProductName/PricePerUnit/Unit jarayon vaqtidagi qiymatlardir.
    /// </summary>
    public class UsageReportRowDto
    {
        public long ProcessId { get; set; }
        public long SessionId { get; set; }

        public DateTime StartedAt { get; set; }
        public DateTime? EndedAt { get; set; }

        public string ProductName { get; set; } = string.Empty;
        public string Unit { get; set; } = string.Empty;

        public decimal RequestedAmount { get; set; }
        public decimal GivenAmount { get; set; }
        public decimal PricePerUnit { get; set; }
        public decimal TotalCost { get; set; }

        public string Status { get; set; } = string.Empty;
        public string? EndReason { get; set; }

        public string DeviceSerial { get; set; } = string.Empty;
        public string StationName { get; set; } = string.Empty;

        /// <summary>Foydalanuvchining telefoni — tashkilot/merchant scopelarida kim ishlatganini ko'rsatish uchun.</summary>
        public string? UserPhone { get; set; }
    }
}
