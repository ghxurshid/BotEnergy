namespace Domain.Dtos
{
    public class SetPasswordDto
    {
        public required long UserId { get; set; }
        public required string Password { get; set; }
    }

    public class SetPasswordResultDto
    {
        public required string AccessToken { get; set; }
        public required string RefreshToken { get; set; }
        public required DateTime AccessTokenExpiration { get; set; }
    }
}
