namespace AuthApi.Models.Requests
{
    public class ResetPasswordSetRequest
    {
        public required string PhoneNumber { get; set; }
        public required string NewPassword { get; set; }
    }
}
