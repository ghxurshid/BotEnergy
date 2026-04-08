namespace AuthApi.Models.Responses
{
    public class ResetPasswordRequestResponse
    {
        public long UserId { get; set; }
        public required string ResultMessage { get; set; }
    }
}
