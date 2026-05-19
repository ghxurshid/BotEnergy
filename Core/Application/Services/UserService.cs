using Domain.Dtos;
using Domain.Dtos.Base;
using Domain.Entities;
using Domain.Interfaces;
using Domain.Repositories;

namespace Application.Services
{
    public class UserService : IUserService
    {
        private readonly IUserRepository _userRepository;

        public UserService(IUserRepository userRepository)
            => _userRepository = userRepository;

        public async Task<GenericDto<GetUserDto>> GetCurrentUserAsync(long userId)
        {
            var user = await _userRepository.GetByIdAsync(userId);
            if (user is null)
                return GenericDto<GetUserDto>.Error(404, "Foydalanuvchi topilmadi.");

            return GenericDto<GetUserDto>.Success(MapToDto(user));
        }

        internal static GetUserDto MapToDto(UserEntity user) => new()
        {
            Id = user.Id,
            PhoneId = user.PhoneId,
            Mail = user.Mail,
            PhoneNumber = user.PhoneNumber,
            UserType = user.UserType.ToString(),
            // NaturalUser → o'z balansi, LegalUser → tashkilot balansi, MerchantUser → 0.
            Balance = user switch
            {
                NaturalUserEntity natural => natural.Balance,
                LegalUserEntity legal => legal.Organization?.Balance ?? 0m,
                _ => 0m
            },
            IsVerified = user.IsVerified,
            IsBlocked = user.IsBlocked,
            LastLoginDate = user.LastLoginDate,
            LastActiveDate = user.LastActiveDate,
            CreatedDate = user.CreatedDate
        };

        public async Task<GenericDto<GetBalanceResultDto>> GetMyBalanceAsync(long userId)
        {
            var user = await _userRepository.GetByIdAsync(userId);
            if (user is null)
                return GenericDto<GetBalanceResultDto>.Error(404, "Foydalanuvchi topilmadi.");

            var balance = user switch
            {
                NaturalUserEntity natural => natural.Balance,
                LegalUserEntity legal => legal.Organization?.Balance ?? 0m,
                _ => 0m
            };

            return GenericDto<GetBalanceResultDto>.Success(new GetBalanceResultDto
            {
                UserId = userId,
                Balance = balance
            });
        }

        public async Task<GenericDto<UpdateUserResultDto>> UpdateCurrentUserAsync(long userId, UpdateUserDto dto)
        {
            var user = await _userRepository.GetByIdAsync(userId);
            if (user is null)
                return GenericDto<UpdateUserResultDto>.Error(404, "Foydalanuvchi topilmadi.");

            if (!string.IsNullOrEmpty(dto.Mail))
                user.Mail = dto.Mail;

            if (!string.IsNullOrEmpty(dto.PhoneId))
                user.PhoneId = dto.PhoneId;

            await _userRepository.UpdateUserAsync(user);

            return GenericDto<UpdateUserResultDto>.Success(new UpdateUserResultDto
            {
                ResultMessage = "Ma'lumotlar muvaffaqiyatli yangilandi."
            });
        }
    }
}
