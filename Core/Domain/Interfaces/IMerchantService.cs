using Domain.Auth;
using Domain.Dtos;
using Domain.Dtos.Base;

namespace Domain.Interfaces
{
    public interface IMerchantService
    {
        Task<GenericDto<MerchantResultDto>> CreateAsync(CreateMerchantDto dto);
        Task<GenericDto<PagedResult<MerchantItemDto>>> GetAllAsync(PaginationParams param, AccessScope scope);
        Task<GenericDto<MerchantItemDto>> GetByIdAsync(long id, AccessScope scope);
        Task<GenericDto<MerchantResultDto>> UpdateAsync(long id, UpdateMerchantDto dto, AccessScope scope);
        Task<GenericDto<MerchantResultDto>> DeleteAsync(long id, AccessScope scope);

        /// <summary>Merchant Payme kassa credential'larini o'rnatadi (write-only, GET'da masked).</summary>
        Task<GenericDto<MerchantResultDto>> SetPaymeCredentialsAsync(long id, SetPaymeCredentialsDto dto, AccessScope scope);

        /// <summary>Payme Merchant API credential'lari (kassa credential'laridan alohida).</summary>
        Task<GenericDto<MerchantResultDto>> SetPaymeMerchantCredentialsAsync(long id, SetPaymeMerchantCredentialsDto dto, AccessScope scope);

        /// <summary>
        /// To'lov strategiyasini almashtiradi — RUNTIME sozlama, restart talab qilmaydi.
        /// O'zgarish keyingi sessiyalarga ta'sir qiladi.
        /// </summary>
        Task<GenericDto<MerchantResultDto>> SetPaymentMethodsAsync(long id, SetPaymentMethodsDto dto, AccessScope scope);
    }
}
