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
                IsActive = true
            };

            var created = await _repo.CreateAsync(merchant);

            return GenericDto<MerchantResultDto>.Success(new MerchantResultDto
            {
                Id = created.Id,
                ResultMessage = "Merchant muvaffaqiyatli qo'shildi."
            });
        }

        public async Task<GenericDto<List<MerchantItemDto>>> GetAllAsync()
        {
            var list = await _repo.GetAllAsync();
            return GenericDto<List<MerchantItemDto>>.Success(list.Select(ToItem).ToList());
        }

        public async Task<GenericDto<MerchantItemDto>> GetByIdAsync(long id)
        {
            var merchant = await _repo.GetByIdAsync(id);
            if (merchant is null)
                return GenericDto<MerchantItemDto>.Error(404, "Merchant topilmadi.");

            return GenericDto<MerchantItemDto>.Success(ToItem(merchant));
        }

        public async Task<GenericDto<MerchantResultDto>> UpdateAsync(long id, UpdateMerchantDto dto)
        {
            var merchant = await _repo.GetByIdAsync(id);
            if (merchant is null)
                return GenericDto<MerchantResultDto>.Error(404, "Merchant topilmadi.");

            if (!string.IsNullOrWhiteSpace(dto.PhoneNumber)) merchant.PhoneNumber = dto.PhoneNumber;
            if (!string.IsNullOrWhiteSpace(dto.BankAccount)) merchant.BankAccount = dto.BankAccount;
            if (!string.IsNullOrWhiteSpace(dto.CompanyName)) merchant.CompanyName = dto.CompanyName;

            await _repo.UpdateAsync(merchant);

            return GenericDto<MerchantResultDto>.Success(new MerchantResultDto
            {
                Id = merchant.Id,
                ResultMessage = "Merchant ma'lumotlari yangilandi."
            });
        }

        public async Task<GenericDto<MerchantResultDto>> DeleteAsync(long id)
        {
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

        private static MerchantItemDto ToItem(MerchantEntity c) => new()
        {
            Id = c.Id,
            PhoneNumber = c.PhoneNumber,
            Inn = c.Inn,
            BankAccount = c.BankAccount,
            CompanyName = c.CompanyName,
            IsActive = c.IsActive,
            CreatedDate = c.CreatedDate
        };
    }
}
