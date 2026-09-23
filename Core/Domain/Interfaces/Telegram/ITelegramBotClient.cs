namespace Domain.Interfaces.Telegram
{
    /// <summary>
    /// Telegram Bot API bilan ishlaydigan minimal klient.
    /// <see cref="Payme"/> klienti kabi **hech qachon istisno tashlamaydi** —
    /// tarmoq yoki API xatosi <c>false</c> / bo'sh ro'yxat bilan qaytadi va logga tushadi.
    /// </summary>
    public interface ITelegramBotClient
    {
        /// <summary>Token berilganmi (bo'lmasa integratsiya jim turadi).</summary>
        bool IsConfigured { get; }

        /// <summary>Oddiy matnli xabar yuborish.</summary>
        Task<bool> SendMessageAsync(long chatId, string text, CancellationToken ct = default);

        /// <summary>
        /// "Telefon raqamni ulashish" tugmasi bilan xabar yuborish — foydalanuvchi
        /// raqamini Telegram o'zi tasdiqlab yuboradi, ya'ni uni qalbakilashtirib bo'lmaydi.
        /// </summary>
        Task<bool> RequestContactAsync(long chatId, string text, string buttonText, CancellationToken ct = default);

        /// <summary>
        /// Yangi xabarlarni olish (long polling). <paramref name="offset"/> — oxirgi
        /// ishlangan update id + 1.
        /// </summary>
        Task<IReadOnlyList<TelegramUpdate>> GetUpdatesAsync(long offset, int timeoutSeconds, CancellationToken ct = default);
    }

    /// <summary>Telegram'dan kelgan xabarning bizga kerak bo'lgan qismi.</summary>
    public sealed record TelegramUpdate(
        long UpdateId,
        long? ChatId,
        long? FromUserId,
        string? Text,
        string? ContactPhone,
        long? ContactUserId);
}
