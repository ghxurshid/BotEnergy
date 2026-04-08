namespace AuthApi.Models.Requests
{
    public class SetPasswordRequest
    {
        public required long UserId { get; set; }
        public required string Password { get; set; }
    }
}
