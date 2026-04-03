namespace AuthApi.Models.Responses
{
    public class SetPasswordResponse
    {
        public required string AccessToken { get; set; }
        public required string RefreshToken { get; set; }
        public required DateTime AccessTokenExpiration { get; set; }
    }
}
