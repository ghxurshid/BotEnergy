using Domain.Dtos;
using Domain.Dtos.Base;

namespace Domain.Interfaces
{
    /// <summary>
    /// Joriy (login qilgan) platform userning O'Z profilini boshqarish.
    /// Boshqa userlarni boshqarish <see cref="IUserAdminService"/>da — bu servis faqat
    /// caller'ning o'ziga tegishli: profilni ko'rish, emailni yangilash, parolni o'zgartirish.
    /// </summary>
    public interface IPlatformProfileService
    {
        Task<GenericDto<MyProfileDto>> GetMeAsync(long userId);
        Task<GenericDto<MyProfileDto>> UpdateEmailAsync(long userId, string mail);

        /// <summary>Parolni almashtirish — joriy parol tekshiriladi, mos kelsa yangisi o'rnatiladi.</summary>
        Task<GenericDto<UserAdminResultDto>> ChangePasswordAsync(long userId, string currentPassword, string newPassword);
    }
}
