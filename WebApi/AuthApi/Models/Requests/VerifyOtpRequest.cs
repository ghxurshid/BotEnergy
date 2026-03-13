namespace AuthApi.Models.Requests
{
    public class VerifyOtpRequest
    {
        public string PhoneNumber { get; set; }
        public string OtpCode { get; set; }
    }
}
