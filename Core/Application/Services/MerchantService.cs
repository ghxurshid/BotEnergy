using Domain.Auth;
using Domain.Dtos;
using Domain.Dtos.Base;
using Domain.Entities;
using Domain.Guards;
using Domain.Interfaces;
using Domain.Repositories;

namespace Application.Services
{
    public class MerchantService : IMerchantService
    {
        private readonly IMerchantRepository _repo;
        private readonly IUsageProbeRepository _usageProbe;

        public MerchantService(IMerchantRepository repo, IUsageProbeRepository usageProbe)
        {
            _repo = repo;
            _usageProbe = usageProbe;
        }

        public async Task<GenericDto<MerchantResultDto>> CreateAsync(CreateMerchantDto dto)
        {
            var stop = await StopFactorCheck.For(StopActions.MerchantCreate)
                .StopIfAsync(async () => await _repo.GetByPhoneNumberAsync(dto.PhoneNumber) is not null,
                             StopFactors.Merchant.PhoneTaken)
                // inn ustunida ham unique indeks bor — telefon kabi oldindan tekshiramiz.
                .StopIfAsync(() => _repo.ExistsByInnAsync(dto.Inn), StopFactors.Merchant.InnTaken)
                .ResultAsync();

            if (stop is not null)
                return GenericDto<MerchantResultDto>.Blocked(stop);

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
                return GenericDto<MerchantItemDto>.Blocked(StopFactors.Merchant.OutOfScope);

            var merchant = await _repo.GetByIdAsync(id);
            if (merchant is null)
                return GenericDto<MerchantItemDto>.Blocked(StopFactors.Merchant.NotFound);

            return GenericDto<MerchantItemDto>.Success(ToItem(merchant));
        }

        public async Task<GenericDto<MerchantResultDto>> UpdateAsync(long id, UpdateMerchantDto dto, AccessScope scope)
        {
            var found = await _repo.GetByIdAsync(id);

            var stop = await StopFactorCheck.For(StopActions.MerchantUpdate)
                .StopIf(!scope.CanAccessMerchant(id), StopFactors.Merchant.OutOfScope)
                .StopIf(found is null, StopFactors.Merchant.NotFound)
                // Merchantni nofaollashtirish uning barcha stansiya/qurilmalarini biznesdan
                // chiqaradi — ketayotgan sessiyalar tugamay qolardi.
                .StopIfAsync(async () => dto.IsActive == false && found!.IsActive
                                         && await _usageProbe.MerchantHasActiveSessionAsync(id),
                             StopFactors.Merchant.HasActiveSession)
                .ResultAsync();

            if (stop is not null)
                return GenericDto<MerchantResultDto>.Blocked(stop);

            var merchant = found!;

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
            var merchant = await _repo.GetByIdAsync(id);

            var stop = await StopFactorCheck.For(StopActions.MerchantDelete)
                .StopIf(!scope.CanAccessMerchant(id), StopFactors.Merchant.OutOfScope)
                .StopIf(merchant is null, StopFactors.Merchant.NotFound)
                // Soft-delete kaskad qilmaydi — bog'liq yozuvlar egasiz qolib ketardi.
                .StopIfCountAsync(() => _usageProbe.MerchantStationCountAsync(id),
                                  StopFactors.Merchant.HasStations)
                .StopIfCountAsync(() => _usageProbe.MerchantOperatorCountAsync(id),
                                  StopFactors.Merchant.HasUsers)
                .ResultAsync();

            if (stop is not null)
                return GenericDto<MerchantResultDto>.Blocked(stop);

            await _repo.DeleteAsync(id);

            return GenericDto<MerchantResultDto>.Success(new MerchantResultDto
            {
                Id = id,
                ResultMessage = "Merchant o'chirildi."
            });
        }

        public async Task<GenericDto<MerchantResultDto>> SetPaymeCredentialsAsync(long id, SetPaymeCredentialsDto dto, AccessScope scope)
        {
            var found = await _repo.GetByIdAsync(id);

            var stop = StopFactorCheck.For(StopActions.MerchantSetPayme)
                .StopIf(!scope.CanAccessMerchant(id), StopFactors.Merchant.OutOfScope)
                .StopIf(string.IsNullOrWhiteSpace(dto.CashboxId) || string.IsNullOrWhiteSpace(dto.Key),
                        new StopFactor("MERCHANT_PAYME_INCOMPLETE", "CashboxId va Key majburiy.", 400))
                .StopIf(found is null, StopFactors.Merchant.NotFound)
                // Nofaol merchant nomidan invoice yaratilmaydi — credential yozish ma'nosiz.
                .StopIf(() => !found!.IsActive, StopFactors.Merchant.Inactive)
                .Result();

            if (stop is not null)
                return GenericDto<MerchantResultDto>.Blocked(stop);

            var merchant = found!;

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
