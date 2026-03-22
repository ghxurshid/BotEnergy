namespace AuthApi.Models.Requests
{
    public class VerifyRequest
    {
        public required string PhoneNumber { get; set; }
        public required string OtpCode { get; set; }
    }
}
