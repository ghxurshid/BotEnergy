using Domain.Dtos;
using Domain.Dtos.Base;
using Domain.Entities;
using Domain.Enums;
using Domain.Interfaces;
using Domain.Repositories;

namespace Application.Services
{
    public class UserService : IUserService
    {
        private readonly ICustomerUserRepository _userRepository;

        public UserService(ICustomerUserRepository userRepository)
            => _userRepository = userRepository;

        public async Task<GenericDto<GetUserDto>> GetCurrentUserAsync(long userId)
        {
            var user = await _userRepository.GetByIdAsync(userId);
            if (user is null)
                return GenericDto<GetUserDto>.Error(404, "Foydalanuvchi topilmadi.");

            return GenericDto<GetUserDto>.Success(MapToDto(user));
        }

        internal static GetUserDto MapToDto(CustomerUserEntity user) => new()
        {
            Id = user.Id,
            PhoneId = user.PhoneId,
            Mail = user.Mail,
            PhoneNumber = user.PhoneNumber,
            UserType = user.Type.ToString(),
            // Natural → o'z balansi, Corporate → tashkilot balansi.
            Balance = ResolveBalance(user),
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

            return GenericDto<GetBalanceResultDto>.Success(new GetBalanceResultDto
            {
                UserId = userId,
                Balance = ResolveBalance(user)
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

            await _userRepository.UpdateAsync(user);

            return GenericDto<UpdateUserResultDto>.Success(new UpdateUserResultDto
            {
                ResultMessage = "Ma'lumotlar muvaffaqiyatli yangilandi."
            });
        }

        private static decimal ResolveBalance(CustomerUserEntity user) => user.Type switch
        {
            CustomerUserType.Natural => user.Balance,
            CustomerUserType.Corporate => user.Organization?.Balance ?? 0m,
            _ => 0m
        };
    }
}
