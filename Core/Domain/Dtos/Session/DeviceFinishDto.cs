namespace Domain.Dtos.Session
{
    public class DeviceFinishDto
    {
        public string SessionToken { get; set; } = string.Empty;
        public long DeviceId { get; set; }
        public decimal FinalQuantity { get; set; }
    }

    public class DeviceFinishResultDto
    {
        public string ResultMessage { get; set; } = string.Empty;
        public decimal TotalDelivered { get; set; }
    }
}
