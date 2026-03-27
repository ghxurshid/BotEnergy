namespace AuthApi.Models.Requests
{
    public class ResetPasswordVerifyRequest
    {
        public required string PhoneNumber { get; set; }
        public required string OtpCode { get; set; }
    }
}
