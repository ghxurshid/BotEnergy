namespace Domain.Dtos.Session
{
    public class CreateSessionDto
    {
        public long UserId { get; set; }
    }

    /// <summary>
    /// Pending sessiya yaratilgandan keyin client'ga qaytariladigan ma'lumot.
    /// QR kodda <c>UserId + SessionToken</c> jamlanadi — qurilma reader ikkalasini ham o'qiydi.
    /// SessionId yo'q — DB'da sessiya hali yaratilmagan (DeviceApi keyinroq yaratadi).
    /// </summary>
    public class CreateSessionResultDto
    {
        public long UserId { get; set; }
        public string SessionToken { get; set; } = string.Empty;
        public DateTime IdleAfter { get; set; }
        public string ResultMessage { get; set; } = string.Empty;
    }
}
