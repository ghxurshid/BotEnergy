namespace Domain.Dtos.Session
{
    public class SessionProgressDto
    {
        public string SessionToken { get; set; } = string.Empty;
        public long DeviceId { get; set; }
        public decimal Quantity { get; set; }
        public decimal TotalQuantity { get; set; }
    }

    public class SessionProgressResultDto
    {
        public string ResultMessage { get; set; } = string.Empty;
    }
}
