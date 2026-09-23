namespace Domain.Dtos.Telegram
{
    /// <summary>Ilovaga beriladigan "botga o'tish" havolasi.</summary>
    public class TelegramLinkDto
    {
        /// <summary>To'liq havola: <c>https://t.me/{bot}?start={token}</c>.</summary>
        public string Url { get; set; } = string.Empty;

        public string BotUsername { get; set; } = string.Empty;

        public DateTime ExpiresAt { get; set; }

        /// <summary>Profilga Telegram allaqachon bog'langanmi (kodlar darhol keladi).</summary>
        public bool IsLinked { get; set; }
    }
}
