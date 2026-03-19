namespace AuthApi.Models.Requests
{
    public class LoginRequest
    {
        public required string PhoneNumber { get; set; }
        public required string Password { get; set; }
    }
}
