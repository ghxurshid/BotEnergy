namespace AuthApi.Models.Requests
{
    public class SetPasswordRequest
    {
        public required string PhoneNumber { get; set; }
        public required string Password { get; set; }
    }
}
