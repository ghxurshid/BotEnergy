namespace Domain.Dtos.Session
{
    public class SetQuantityDto
    {
        public long SessionId { get; set; }
        public long UserId { get; set; }
        public decimal? RequestedQuantity { get; set; }
    }

    public class SetQuantityResultDto
    {
        public decimal LimitQuantity { get; set; }
        public string ProductName { get; set; } = string.Empty;
        public string Unit { get; set; } = string.Empty;
        public decimal PricePerUnit { get; set; }
        public string DeviceSerialNumber { get; set; } = string.Empty;
        public long ProductId { get; set; }
        public string ResultMessage { get; set; } = string.Empty;
    }
}
