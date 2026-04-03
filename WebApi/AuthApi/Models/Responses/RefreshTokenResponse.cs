namespace AuthApi.Models.Responses
{
    public class RefreshTokenResponse
    {
        public required string AccessToken { get; set; }
        public required string RefreshToken { get; set; }
        public required DateTime AccessTokenExpiration { get; set; }
        public required string ResultMessage { get; set; }
    }
}
