namespace SessionApi.Models.Responses
{
    /// <summary>
    /// Pending sessiya yaratilgandan keyin client'ga qaytariladi.
    /// QR kodda <c>UserId + SessionToken</c> jamlanadi.
    /// </summary>
    public class CreateSessionResponse
    {
        public long UserId { get; set; }
        public string SessionToken { get; set; } = string.Empty;
        public DateTime IdleAfter { get; set; }
        public string Message { get; set; } = string.Empty;
    }
}
