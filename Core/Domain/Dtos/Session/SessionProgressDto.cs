namespace Domain.Dtos.Session
{
    public class SessionProgressDto
    {
        public string SessionToken { get; set; } = string.Empty;
        public string SerialNumber { get; set; } = string.Empty;
        public decimal Quantity { get; set; }
    }

    public class SessionProgressResultDto
    {
        public string ResultMessage { get; set; } = string.Empty;
    }
}
