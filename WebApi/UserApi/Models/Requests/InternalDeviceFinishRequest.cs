namespace UserApi.Models.Requests
{
    public class InternalDeviceFinishRequest
    {
        public string SerialNumber { get; set; } = string.Empty;
        public string SessionToken { get; set; } = string.Empty;
        public decimal FinalQuantity { get; set; }
    }
}
