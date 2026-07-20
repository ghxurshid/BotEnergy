namespace AdminApi.Models.Requests
{
    public class SetPasswordRequest
    {
        public string Password { get; set; } = string.Empty;

        /// <summary>Amalni bajarayotgan adminning o'z JORIY paroli (majburiy).</summary>
        public string CurrentPassword { get; set; } = string.Empty;
    }
}
