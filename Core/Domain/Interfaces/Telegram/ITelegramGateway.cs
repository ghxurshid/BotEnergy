using Domain.Dtos.Base;
using Domain.Dtos.Telegram;
using Domain.Enums;

namespace Domain.Interfaces.Telegram
{
    /// <summary>
    /// Telegram orqali tasdiqlash kodlarini yetkazish oqimi.
    ///
    /// 1. Ilova <see cref="CreateLinkAsync"/> bilan havola oladi va foydalanuvchini botga olib o'tadi.
    /// 2. Foydalanuvchi "Start" bosadi → <see cref="HandleUpdateAsync"/> ishlaydi:
    ///    bot telefon raqamni so'raydi, Telegram tasdiqlagan raqam profil raqami bilan
    ///    solishtiriladi va chat profilga bog'lanadi.
    /// 3. Keyingi barcha OTP'lar <see cref="SendOtpAsync"/> orqali shu chatga boradi.
    /// </summary>
    public interface ITelegramGateway
    {
        /// <summary>Profil uchun bir martalik "botga o'tish" havolasi.</summary>
        Task<GenericDto<TelegramLinkDto>> CreateLinkAsync(long userId);

        /// <summary>Telegram'dan kelgan bitta xabarni qayta ishlaydi.</summary>
        Task HandleUpdateAsync(TelegramUpdate update, CancellationToken ct = default);

        /// <summary>
        /// Tasdiqlash kodini foydalanuvchining Telegram chatiga yuboradi.
        /// Chat bog'lanmagan bo'lsa <c>false</c> qaytadi (oqim buzilmaydi).
        /// </summary>
        Task<bool> SendOtpAsync(string phoneNumber, string code, OtpPurpose purpose, CancellationToken ct = default);
    }
}
