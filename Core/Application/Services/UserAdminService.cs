using Application.Helpers;
using Domain.Constants;
using Domain.Dtos;
using Domain.Dtos.Base;
using Domain.Entities;
using Domain.Interfaces;
using Domain.Repositories;

namespace Application.Services
{
    public class UserAdminService : IUserAdminService
    {
        private readonly IUserRepository _userRepo;
        private readonly IRoleRepository _roleRepo;
        private readonly IOrganizationRepository _orgRepo;
        private readonly IStationRepository _stationRepo;

        public UserAdminService(
            IUserRepository userRepo,
            IRoleRepository roleRepo,
            IOrganizationRepository orgRepo,
            IStationRepository stationRepo)
        {
            _userRepo = userRepo;
            _roleRepo = roleRepo;
            _orgRepo = orgRepo;
            _stationRepo = stationRepo;
        }

        public async Task<GenericDto<UserAdminResultDto>> CreateAsync(CreateUserAdminDto dto, long callerId, HashSet<string> callerPermissions)
        {
            var existingUser = await _userRepo.GetByPhoneNumberAsync(dto.PhoneNumber);
            if (existingUser is not null)
                return GenericDto<UserAdminResultDto>.Error(409, "Bu telefon raqam bilan foydalanuvchi allaqachon mavjud.");

            var role = await _roleRepo.GetByIdAsync(dto.RoleId);
            if (role is null)
                return GenericDto<UserAdminResultDto>.Error(404, "Rol topilmadi.");

            if (dto.OrganizationId.HasValue && dto.StationId.HasValue)
                dto.StationId = null;

            UserEntity newUser;

            if (dto.OrganizationId.HasValue)
            {
                var org = await _orgRepo.GetByIdAsync(dto.OrganizationId.Value);
                if (org is null)
                    return GenericDto<UserAdminResultDto>.Error(404, "Tashkilot topilmadi.");

                if (!org.IsActive)
                    return GenericDto<UserAdminResultDto>.Error(400, "Tashkilot faol emas.");

                if (!callerPermissions.Contains(Permissions.OrganizationAdminCreate))
                {
                    var caller = await _userRepo.GetByIdAsync(callerId);
                    if (caller is LegalUserEntity legalUser && legalUser.OrganizationId != dto.OrganizationId)
                        return GenericDto<UserAdminResultDto>.Error(403, "Faqat o'z tashkilotingizga foydalanuvchi qo'sha olasiz.");
                }

                newUser = new LegalUserEntity
                {
                    PhoneId = dto.PhoneId,
                    PhoneNumber = dto.PhoneNumber,
                    Mail = dto.Mail,
                    RoleId = dto.RoleId,
                    OrganizationId = dto.OrganizationId.Value,
                    IsOtpVerified = true,
                    IsVerified = false
                };
            }
            else if (dto.StationId.HasValue)
            {
                var station = await _stationRepo.GetByIdAsync(dto.StationId.Value);
                if (station is null)
                    return GenericDto<UserAdminResultDto>.Error(404, "Stansiya topilmadi.");

                if (!station.IsActive)
                    return GenericDto<UserAdminResultDto>.Error(400, "Stansiya faol emas.");

                if (!callerPermissions.Contains(Permissions.MerchantAdminRegister))
                {
                    var caller = await _userRepo.GetByIdAsync(callerId);
                    if (caller is MerchantUserEntity merchantUser)
                    {
                        var callerStation = await _stationRepo.GetByIdAsync(merchantUser.StationId);
                        if (callerStation?.MerchantId != station.MerchantId)
                            return GenericDto<UserAdminResultDto>.Error(403, "Faqat o'z merchantingizga foydalanuvchi qo'sha olasiz.");
                    }
                }

                newUser = new MerchantUserEntity
                {
                    PhoneId = dto.PhoneId,
                    PhoneNumber = dto.PhoneNumber,
                    Mail = dto.Mail,
                    RoleId = dto.RoleId,
                    StationId = dto.StationId.Value,
                    IsOtpVerified = true,
                    IsVerified = false
                };
            }
            else
            {
                return GenericDto<UserAdminResultDto>.Error(400,
                    "OrganizationId yoki StationId dan biri ko'rsatilishi shart.");
            }

            var created = await _userRepo.CreateUserAsync(newUser);

            return GenericDto<UserAdminResultDto>.Success(new UserAdminResultDto
            {
                Id = created.Id,
                ResultMessage = "Foydalanuvchi muvaffaqiyatli yaratildi."
            });
        }

        public async Task<GenericDto<UserAdminResultDto>> SetPasswordAsync(SetPasswordAdminDto dto)
        {
            var user = await _userRepo.GetByIdAsync(dto.UserId);
            if (user is null)
                return GenericDto<UserAdminResultDto>.Error(404, "Foydalanuvchi topilmadi.");

            if (user.IsVerified)
                return GenericDto<UserAdminResultDto>.Error(400, "Parol allaqachon o'rnatilgan.");

            if (!user.IsOtpVerified)
                return GenericDto<UserAdminResultDto>.Error(400, "OTP tasdiqlanmagan.");

            var (hash, salt) = PasswordHelper.CreatePassword(dto.Password);
            user.PasswordHash = hash;
            user.PasswordSalt = salt;
            user.IsVerified = true;
            await _userRepo.UpdateUserAsync(user);

            return GenericDto<UserAdminResultDto>.Success(new UserAdminResultDto
            {
                Id = user.Id,
                ResultMessage = "Parol muvaffaqiyatli o'rnatildi."
            });
        }

        public async Task<GenericDto<UserAdminResultDto>> ResetPasswordAsync(ResetPasswordAdminDto dto)
        {
            var user = await _userRepo.GetByIdAsync(dto.UserId);
            if (user is null)
                return GenericDto<UserAdminResultDto>.Error(404, "Foydalanuvchi topilmadi.");

            if (!user.IsVerified)
                return GenericDto<UserAdminResultDto>.Error(400, "Foydalanuvchi hali ro'yxatdan to'liq o'tmagan.");

            var (hash, salt) = PasswordHelper.CreatePassword(dto.NewPassword);
            user.PasswordHash = hash;
            user.PasswordSalt = salt;
            await _userRepo.UpdateUserAsync(user);

            return GenericDto<UserAdminResultDto>.Success(new UserAdminResultDto
            {
                Id = user.Id,
                ResultMessage = "Parol muvaffaqiyatli yangilandi."
            });
        }

        public async Task<GenericDto<List<UserAdminItemDto>>> GetAllAsync()
        {
            var users = await _userRepo.GetAllAsync();
            var items = users.Select(ToItem).ToList();
            return GenericDto<List<UserAdminItemDto>>.Success(items);
        }

        public async Task<GenericDto<UserAdminItemDto>> GetByIdAsync(long userId)
        {
            var user = await _userRepo.GetByIdAsync(userId);
            if (user is null)
                return GenericDto<UserAdminItemDto>.Error(404, "Foydalanuvchi topilmadi.");

            return GenericDto<UserAdminItemDto>.Success(ToItem(user));
        }

        public async Task<GenericDto<UserAdminResultDto>> BlockAsync(long userId)
        {
            var user = await _userRepo.GetByIdAsync(userId);
            if (user is null)
                return GenericDto<UserAdminResultDto>.Error(404, "Foydalanuvchi topilmadi.");

            if (user.IsBlocked)
                return GenericDto<UserAdminResultDto>.Error(400, "Foydalanuvchi allaqachon bloklangan.");

            user.IsBlocked = true;
            await _userRepo.UpdateUserAsync(user);

            return GenericDto<UserAdminResultDto>.Success(new UserAdminResultDto
            {
                Id = user.Id,
                ResultMessage = "Foydalanuvchi bloklandi."
            });
        }

        public async Task<GenericDto<UserAdminResultDto>> UnblockAsync(long userId)
        {
            var user = await _userRepo.GetByIdAsync(userId);
            if (user is null)
                return GenericDto<UserAdminResultDto>.Error(404, "Foydalanuvchi topilmadi.");

            if (!user.IsBlocked)
                return GenericDto<UserAdminResultDto>.Error(400, "Foydalanuvchi bloklanmagan.");

            user.IsBlocked = false;
            await _userRepo.UpdateUserAsync(user);

            return GenericDto<UserAdminResultDto>.Success(new UserAdminResultDto
            {
                Id = user.Id,
                ResultMessage = "Foydalanuvchi blokdan chiqarildi."
            });
        }

        public async Task<GenericDto<UserAdminResultDto>> DeleteAsync(long userId)
        {
            var user = await _userRepo.GetByIdAsync(userId);
            if (user is null)
                return GenericDto<UserAdminResultDto>.Error(404, "Foydalanuvchi topilmadi.");

            await _userRepo.DeleteUserAsync(userId);

            return GenericDto<UserAdminResultDto>.Success(new UserAdminResultDto
            {
                Id = userId,
                ResultMessage = "Foydalanuvchi o'chirildi."
            });
        }

        private static UserAdminItemDto ToItem(UserEntity u)
        {
            decimal balance = u switch
            {
                NaturalUserEntity n => n.Balance,
                LegalUserEntity l => l.Organization?.Balance ?? 0,
                _ => 0
            };

            return new UserAdminItemDto
            {
                Id = u.Id,
                PhoneNumber = u.PhoneNumber,
                Mail = u.Mail,
                UserType = u.UserType,
                IsVerified = u.IsVerified,
                IsBlocked = u.IsBlocked,
                RoleId = u.RoleId,
                RoleName = u.Role?.Name,
                Balance = balance,
                CreatedDate = u.CreatedDate,
                LastLoginDate = u.LastLoginDate
            };
        }
    }
}
