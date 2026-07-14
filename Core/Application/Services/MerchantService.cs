using Domain.Auth;
using Domain.Dtos;
using Domain.Dtos.Base;
using Domain.Entities;
using Domain.Interfaces;
using Domain.Repositories;

namespace Application.Services
{
    public class MerchantService : IMerchantService
    {
        private readonly IMerchantRepository _repo;

        public MerchantService(IMerchantRepository repo)
            => _repo = repo;

        public async Task<GenericDto<MerchantResultDto>> CreateAsync(CreateMerchantDto dto)
        {
            var existing = await _repo.GetByPhoneNumberAsync(dto.PhoneNumber);
            if (existing is not null)
                return GenericDto<MerchantResultDto>.Error(409, "Bu telefon raqam bilan merchant allaqachon mavjud.");

            var merchant = new MerchantEntity
            {
                PhoneNumber = dto.PhoneNumber,
                Inn = dto.Inn,
                BankAccount = dto.BankAccount,
                CompanyName = dto.CompanyName,
                IsActive = dto.IsActive
            };

            var created = await _repo.CreateAsync(merchant);

            return GenericDto<MerchantResultDto>.Success(new MerchantResultDto
            {
                Id = created.Id,
                ResultMessage = "Merchant muvaffaqiyatli qo'shildi."
            });
        }

        public async Task<GenericDto<PagedResult<MerchantItemDto>>> GetAllAsync(PaginationParams param, AccessScope scope)
        {
            // Platform → hammasi; merchant user → faqat o'z merchanti; aks holda bo'sh.
            if (!scope.IsManage && scope.MerchantId is null)
                return GenericDto<PagedResult<MerchantItemDto>>.Success(PagedResult<MerchantItemDto>.Empty(param));

            var page = await _repo.GetAllAsync(param, scope.IsManage ? null : scope.MerchantId);
            return GenericDto<PagedResult<MerchantItemDto>>.Success(page.Map(ToItem));
        }

        public async Task<GenericDto<MerchantItemDto>> GetByIdAsync(long id, AccessScope scope)
        {
            if (!scope.CanAccessMerchant(id))
                return GenericDto<MerchantItemDto>.Error(403, "Bu merchant sizning doirangizga tegishli emas.");

            var merchant = await _repo.GetByIdAsync(id);
            if (merchant is null)
                return GenericDto<MerchantItemDto>.Error(404, "Merchant topilmadi.");

            return GenericDto<MerchantItemDto>.Success(ToItem(merchant));
        }

        public async Task<GenericDto<MerchantResultDto>> UpdateAsync(long id, UpdateMerchantDto dto, AccessScope scope)
        {
            if (!scope.CanAccessMerchant(id))
                return GenericDto<MerchantResultDto>.Error(403, "Bu merchant sizning doirangizga tegishli emas.");

            var merchant = await _repo.GetByIdAsync(id);
            if (merchant is null)
                return GenericDto<MerchantResultDto>.Error(404, "Merchant topilmadi.");

            if (!string.IsNullOrWhiteSpace(dto.PhoneNumber)) merchant.PhoneNumber = dto.PhoneNumber;
            if (dto.IsActive.HasValue) merchant.IsActive = dto.IsActive.Value;

            await _repo.UpdateAsync(merchant);

            return GenericDto<MerchantResultDto>.Success(new MerchantResultDto
            {
                Id = merchant.Id,
                ResultMessage = "Merchant ma'lumotlari yangilandi."
            });
        }

        public async Task<GenericDto<MerchantResultDto>> DeleteAsync(long id, AccessScope scope)
        {
            if (!scope.CanAccessMerchant(id))
                return GenericDto<MerchantResultDto>.Error(403, "Bu merchant sizning doirangizga tegishli emas.");

            var merchant = await _repo.GetByIdAsync(id);
            if (merchant is null)
                return GenericDto<MerchantResultDto>.Error(404, "Merchant topilmadi.");

            await _repo.DeleteAsync(id);

            return GenericDto<MerchantResultDto>.Success(new MerchantResultDto
            {
                Id = id,
                ResultMessage = "Merchant o'chirildi."
            });
        }

        public async Task<GenericDto<MerchantResultDto>> SetPaymeCredentialsAsync(long id, SetPaymeCredentialsDto dto, AccessScope scope)
        {
            if (!scope.CanAccessMerchant(id))
                return GenericDto<MerchantResultDto>.Error(403, "Bu merchant sizning doirangizga tegishli emas.");

            if (string.IsNullOrWhiteSpace(dto.CashboxId) || string.IsNullOrWhiteSpace(dto.Key))
                return GenericDto<MerchantResultDto>.Error(400, "CashboxId va Key majburiy.");

            var merchant = await _repo.GetByIdAsync(id);
            if (merchant is null)
                return GenericDto<MerchantResultDto>.Error(404, "Merchant topilmadi.");

            merchant.PaymeCashboxId = dto.CashboxId.Trim();
            merchant.PaymeKey = dto.Key.Trim();
            merchant.PaymeEnabled = dto.Enabled;
            await _repo.UpdateAsync(merchant);

            return GenericDto<MerchantResultDto>.Success(new MerchantResultDto
            {
                Id = merchant.Id,
                ResultMessage = "Payme credential'lari saqlandi."
            });
        }

        private static MerchantItemDto ToItem(MerchantEntity c) => new()
        {
            Id = c.Id,
            PhoneNumber = c.PhoneNumber,
            Inn = c.Inn,
            BankAccount = c.BankAccount,
            CompanyName = c.CompanyName,
            IsActive = c.IsActive,
            CreatedDate = c.CreatedDate,
            PaymeCashboxId = c.PaymeCashboxId,
            PaymeKeyMasked = Mask(c.PaymeKey),
            PaymeEnabled = c.PaymeEnabled
        };

        /// <summary>Kalitni maskalab qaytaradi — faqat oxirgi 4 belgi ko'rinadi.</summary>
        private static string? Mask(string? key)
        {
            if (string.IsNullOrEmpty(key)) return null;
            return key.Length <= 4 ? "••••" : "••••" + key[^4..];
        }
    }
}
