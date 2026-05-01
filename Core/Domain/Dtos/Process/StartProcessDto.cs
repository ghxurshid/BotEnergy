namespace Domain.Dtos.Process
{
    /// <summary>
    /// Sessiya ulangandan keyin yangi mahsulot berish jarayonini boshlash.
    /// </summary>
    public class StartProcessDto
    {
        public long SessionId { get; set; }
        public long UserId { get; set; }
        public long ProductId { get; set; }
        public decimal? RequestedAmount { get; set; }
    }

    public class StartProcessResultDto
    {
        public long ProcessId { get; set; }
        public long ProductId { get; set; }
        public string ProductName { get; set; } = string.Empty;
        public string Unit { get; set; } = string.Empty;
        public decimal PricePerUnit { get; set; }
        public decimal LimitAmount { get; set; }
        public string DeviceSerialNumber { get; set; } = string.Empty;
        public string ResultMessage { get; set; } = string.Empty;
    }
}
