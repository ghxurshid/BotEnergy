using Domain.Auth;
using Domain.Dtos;
using Domain.Dtos.Base;
using Domain.Entities;
using Domain.Enums;
using Domain.Guards;
using Domain.Interfaces;
using Domain.Payments;
using Domain.Repositories;
using Microsoft.Extensions.Logging;

namespace Application.Services
{
    public class MerchantService : IMerchantService
    {
        private readonly IMerchantRepository _repo;
        private readonly IUsageProbeRepository _usageProbe;
        private readonly ILogger<MerchantService> _logger;

        public MerchantService(
            IMerchantRepository repo,
            IUsageProbeRepository usageProbe,
            ILogger<MerchantService> logger)
        {
            _repo = repo;
            _usageProbe = usageProbe;
            _logger = logger;
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

        /// <summary>
        /// To'lov strategiyasini almashtirish — RUNTIME sozlama (deploy/restart kerak emas).
        /// Saqlashdan oldin har bir yoqilgan usulning credential'lari tekshiriladi, aks holda
        /// merchant o'zini "to'lov qabul qila olmaydigan" holatga qo'yib qo'yardi.
        /// O'zgarish KEYINGI sessiyalarga ta'sir qiladi.
        /// </summary>
        public async Task<GenericDto<MerchantResultDto>> SetPaymentMethodsAsync(
            long id, SetPaymentMethodsDto dto, AccessScope scope)
        {
            var found = await _repo.GetByIdAsync(id);
            var enabled = dto.EnabledMethods?.Distinct().ToList() ?? new List<PaymentMethod>();
            PaymentMethod? unconfigured = null;

            var stop = StopFactorCheck.For(StopActions.MerchantSetPaymentMethods)
                .StopIf(!scope.CanAccessMerchant(id), StopFactors.Merchant.OutOfScope)
                .StopIf(enabled.Count == 0,
                        new StopFactor("MERCHANT_PAYMENT_METHODS_EMPTY",
                            "Kamida bitta to'lov usuli yoqilishi kerak.", 400))
                .StopIf(!enabled.Contains(dto.DefaultMethod),
                        new StopFactor("MERCHANT_DEFAULT_METHOD_NOT_ENABLED",
                            "Sukut usuli yoqilgan usullar ichida bo'lishi kerak.", 400))
                .StopIf(found is null, StopFactors.Merchant.NotFound)
                .StopIf(() => !found!.IsActive, StopFactors.Merchant.Inactive)
                .StopIf(() =>
                {
                    unconfigured = enabled.FirstOrDefault(m => !PaymentMethodRequirements.IsConfigured(found!, m));
                    return enabled.Any(m => !PaymentMethodRequirements.IsConfigured(found!, m));
                },
                    () => new StopFactor("MERCHANT_PAYMENT_METHOD_NOT_CONFIGURED",
                        $"{unconfigured} usuli sozlanmagan. {PaymentMethodRequirements.MissingRequirement(unconfigured!.Value)}",
                        409))
                .Result();

            if (stop is not null)
                return GenericDto<MerchantResultDto>.Blocked(stop);

            var merchant = found!;

            merchant.DefaultPaymentMethod = dto.DefaultMethod;
            merchant.EnabledPaymentMethods = enabled.Aggregate(
                PaymentMethodFlags.None, (acc, m) => acc | m.ToFlag());
            merchant.RefundUnusedFunds = dto.RefundUnusedFunds;
            await _repo.UpdateAsync(merchant);

            _logger.LogInformation(
                "[PAY] Merchant to'lov usuli o'zgardi merchantId={MerchantId} default={Default} enabled={Enabled}",
                merchant.Id, merchant.DefaultPaymentMethod, merchant.EnabledPaymentMethods);

            return GenericDto<MerchantResultDto>.Success(new MerchantResultDto
            {
                Id = merchant.Id,
                ResultMessage = $"To'lov usuli saqlandi: {dto.DefaultMethod} (keyingi sessiyalardan boshlab)."
            });
        }

        /// <summary>Payme Merchant API credential'lari — kassa credential'laridan alohida.</summary>
        public async Task<GenericDto<MerchantResultDto>> SetPaymeMerchantCredentialsAsync(
            long id, SetPaymeMerchantCredentialsDto dto, AccessScope scope)
        {
            var found = await _repo.GetByIdAsync(id);

            var stop = StopFactorCheck.For(StopActions.MerchantSetPayme)
                .StopIf(!scope.CanAccessMerchant(id), StopFactors.Merchant.OutOfScope)
                .StopIf(string.IsNullOrWhiteSpace(dto.MerchantId) || string.IsNullOrWhiteSpace(dto.Key),
                        new StopFactor("MERCHANT_PAYME_INCOMPLETE", "MerchantId va Key majburiy.", 400))
                .StopIf(found is null, StopFactors.Merchant.NotFound)
                .StopIf(() => !found!.IsActive, StopFactors.Merchant.Inactive)
                .Result();

            if (stop is not null)
                return GenericDto<MerchantResultDto>.Blocked(stop);

            var merchant = found!;

            merchant.PaymeMerchantId = dto.MerchantId.Trim();
            merchant.PaymeMerchantKey = dto.Key.Trim();
            await _repo.UpdateAsync(merchant);

            return GenericDto<MerchantResultDto>.Success(new MerchantResultDto
            {
                Id = merchant.Id,
                ResultMessage = "Payme Merchant API credential'lari saqlandi."
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
            PaymeEnabled = c.PaymeEnabled,
            PaymeMerchantId = c.PaymeMerchantId,
            PaymeMerchantKeyMasked = Mask(c.PaymeMerchantKey),
            DefaultPaymentMethod = c.DefaultPaymentMethod,
            EnabledPaymentMethods = c.EnabledPaymentMethods.ToMethods().ToList(),
            RefundUnusedFunds = c.RefundUnusedFunds,
            ConfigurablePaymentMethods = Enum.GetValues<PaymentMethod>()
                .Where(m => PaymentMethodRequirements.IsConfigured(c, m))
                .ToList()
        };

        /// <summary>Kalitni maskalab qaytaradi — faqat oxirgi 4 belgi ko'rinadi.</summary>
        private static string? Mask(string? key)
        {
            if (string.IsNullOrEmpty(key)) return null;
            return key.Length <= 4 ? "••••" : "••••" + key[^4..];
        }
    }
}
