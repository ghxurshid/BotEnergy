namespace AuthApi.Models.Requests
{
    /// <summary>
    /// Qaysi profil uchun "botga o'tish" havolasi kerakligi.
    /// UserId ro'yxatdan o'tish (Register) yoki parol tiklash (ResetPasswordRequest)
    /// javobida ilovaga qaytariladi.
    /// </summary>
    public class TelegramLinkRequest
    {
        public long UserId { get; set; }
    }
}
