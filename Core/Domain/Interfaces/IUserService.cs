using Domain.Dtos;
using Domain.Dtos.Base;

namespace Domain.Interfaces
{
    public interface IUserService
    {
        Task<GenericDto<GetUserDto>> GetCurrentUserAsync(long userId);
        Task<GenericDto<UpdateUserResultDto>> UpdateCurrentUserAsync(long userId, UpdateUserDto dto);

        /// <summary>
        /// Foydalanuvchining joriy balansini qaytaradi (DB'dan).
        /// NaturalUser → o'z balansi, LegalUser → biriktirilgan tashkilot balansi.
        /// </summary>
        Task<GenericDto<GetBalanceResultDto>> GetMyBalanceAsync(long userId);
    }
}
