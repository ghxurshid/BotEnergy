using Domain.Dtos;
using Domain.Dtos.Base;
using Domain.Entities;
using Domain.Helpers;
using Domain.Interfaces;
using Domain.Repositories;

namespace Application.Services
{
    /// <summary>
    /// Joriy platform userning o'z profili (email + parol). Boshqa userlarga tegmaydi —
    /// har doim faqat <c>userId</c> (JWT'dan) bo'yicha ishlaydi.
    /// </summary>
    public class PlatformProfileService : IPlatformProfileService
    {
        private readonly IPlatformUserRepository _userRepo;

        public PlatformProfileService(IPlatformUserRepository userRepo)
            => _userRepo = userRepo;

        public async Task<GenericDto<MyProfileDto>> GetMeAsync(long userId)
        {
            var user = await _userRepo.GetByIdAsync(userId);
            if (user is null)
                return GenericDto<MyProfileDto>.Error(404, "Foydalanuvchi topilmadi.");

            return GenericDto<MyProfileDto>.Success(ToDto(user));
        }

        public async Task<GenericDto<MyProfileDto>> UpdateEmailAsync(long userId, string mail)
        {
            if (string.IsNullOrWhiteSpace(mail))
                return GenericDto<MyProfileDto>.Error(400, "Email kiritilishi shart.");
            mail = mail.Trim();
            if (!mail.Contains('@') || mail.Length < 5)
                return GenericDto<MyProfileDto>.Error(400, "Email formati noto'g'ri.");

            var user = await _userRepo.GetByIdAsync(userId);
            if (user is null)
                return GenericDto<MyProfileDto>.Error(404, "Foydalanuvchi topilmadi.");

            user.Mail = mail;
            await _userRepo.UpdateAsync(user);

            return GenericDto<MyProfileDto>.Success(ToDto(user));
        }

        public async Task<GenericDto<UserAdminResultDto>> ChangePasswordAsync(long userId, string currentPassword, string newPassword)
        {
            if (string.IsNullOrWhiteSpace(currentPassword))
                return GenericDto<UserAdminResultDto>.Error(400, "Joriy parolni kiriting.");
            if (string.IsNullOrWhiteSpace(newPassword) || newPassword.Length < 6)
                return GenericDto<UserAdminResultDto>.Error(400, "Yangi parol kamida 6 ta belgidan iborat bo'lishi kerak.");

            var user = await _userRepo.GetByIdAsync(userId);
            if (user is null)
                return GenericDto<UserAdminResultDto>.Error(404, "Foydalanuvchi topilmadi.");
            if (user.PasswordHash is null || user.PasswordSalt is null)
                return GenericDto<UserAdminResultDto>.Error(400, "Parol hali o'rnatilmagan.");

            // Joriy parolni tekshirish — mos kelmasa amaliyot bajarilmaydi.
            if (!PasswordHelper.Verify(currentPassword, user.PasswordHash, user.PasswordSalt))
                return GenericDto<UserAdminResultDto>.Error(403, "Joriy parolingiz noto'g'ri.");

            var (hash, salt) = PasswordHelper.CreatePassword(newPassword);
            user.PasswordHash = hash;
            user.PasswordSalt = salt;
            await _userRepo.UpdateAsync(user);

            return GenericDto<UserAdminResultDto>.Success(new UserAdminResultDto
            {
                Id = user.Id,
                ResultMessage = "Parol muvaffaqiyatli o'zgartirildi."
            });
        }

        private static MyProfileDto ToDto(PlatformUserEntity u) => new()
        {
            Id = u.Id,
            PhoneNumber = u.PhoneNumber,
            Mail = u.Mail,
            SubType = u.Type.ToString(),
            MerchantId = u.MerchantId,
            RoleId = u.RoleId,
            RoleName = u.Role?.Name,
            IsVerified = u.IsVerified,
            IsBlocked = u.IsBlocked,
            CreatedDate = u.CreatedDate,
            LastLoginDate = u.LastLoginDate
        };
    }
}
