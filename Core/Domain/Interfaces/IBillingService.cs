using Domain.Dtos;
using Domain.Dtos.Base;

namespace Domain.Interfaces
{
    public interface IBillingService
    {
        Task<GenericDto<GetBalanceResultDto>> GetBalanceAsync(long userId);
        Task<GenericDto<TopUpBalanceResultDto>> TopUpAsync(TopUpBalanceDto dto);
    }
}
