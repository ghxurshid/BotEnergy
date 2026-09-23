namespace Domain.Options
{
    /// <summary>
    /// Telegram bot sozlamalari (<c>Telegram</c> bo'limi).
    /// Token — sir: productionda env orqali beriladi (<c>Telegram__BotToken</c>).
    /// </summary>
    public class TelegramOptions
    {
        public string BaseUrl { get; set; } = "https://api.telegram.org";

        /// <summary>BotFather bergan token. Bo'sh bo'lsa integratsiya o'chiq hisoblanadi.</summary>
        public string BotToken { get; set; } = string.Empty;

        /// <summary>Bot foydalanuvchi nomi (@siz). Havola shu asosda quriladi.</summary>
        public string BotUsername { get; set; } = string.Empty;

        public int TimeoutSeconds { get; set; } = 60;

        /// <summary>Kodlarni Telegram orqali yuborish yoqilganmi.</summary>
        public bool Enabled { get; set; } = true;

        /// <summary>Havola (deep link) tokeni necha daqiqa amal qiladi.</summary>
        public int LinkTtlMinutes { get; set; } = 15;
    }
}
