namespace UserApi.Models.Requests
{
    public class ResetOrgUserPasswordRequest
    {
        public string NewPassword { get; set; } = string.Empty;

        /// <summary>Amalni bajarayotgan adminning o'z JORIY paroli (majburiy).</summary>
        public string CurrentPassword { get; set; } = string.Empty;
    }
}
