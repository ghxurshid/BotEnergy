using Domain.Dtos;
using Domain.Dtos.Base;

namespace Domain.Interfaces
{
    public interface IBillingService
    {
        Task<GenericDto<GetBalanceResultDto>> GetBalanceAsync(long userId);
        Task<GenericDto<TopUpBalanceResultDto>> TopUpAsync(TopUpBalanceDto dto);

        /// <summary>
        /// Foydalanuvchining hozirgi balansini olish (NaturalUser → o'z balansi, LegalUser → Organization).
        /// </summary>
        Task<decimal> GetAvailableBalanceAsync(long userId);

        /// <summary>
        /// Tugagan jarayon uchun balansni atomic yechib olish.
        /// Idempotent: agar jarayon allaqachon yechilgan bo'lsa, qayta yechmaydi.
        /// Balans yetarli bo'lmasa, mavjud miqdorgacha yechib oladi.
        /// </summary>
        Task<decimal> DeductForProcessAsync(long processId);
    }
}
