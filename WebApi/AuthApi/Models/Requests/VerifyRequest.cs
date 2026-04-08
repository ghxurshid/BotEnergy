namespace AuthApi.Models.Requests
{
    public class VerifyRequest
    {
        public required long UserId { get; set; }
        public required string OtpCode { get; set; }
    }
}
