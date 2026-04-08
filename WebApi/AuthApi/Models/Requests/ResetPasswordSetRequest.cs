namespace AuthApi.Models.Requests
{
    public class ResetPasswordSetRequest
    {
        public required long UserId { get; set; }
        public required string NewPassword { get; set; }
    }
}
