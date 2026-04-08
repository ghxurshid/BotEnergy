namespace AuthApi.Models.Requests
{
    public class ResetPasswordVerifyRequest
    {
        public required long UserId { get; set; }
        public required string OtpCode { get; set; }
    }
}
