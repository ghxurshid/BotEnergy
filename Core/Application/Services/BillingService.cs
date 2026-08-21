using Domain.Dtos;
using Domain.Dtos.Base;
using Domain.Entities;
using Domain.Enums;
using Domain.Guards;
using Domain.Interfaces;
using Domain.Repositories;
using Microsoft.Extensions.Logging;

namespace Application.Services
{
    public class BillingService : IBillingService
    {
        private readonly ICustomerUserRepository _userRepo;
        private readonly IOrganizationRepository _orgRepo;
        private readonly IProductProcessRepository _processRepo;
        private readonly ITransactionRunner _tx;
        private readonly ILogger<BillingService> _logger;

        public BillingService(
            ICustomerUserRepository userRepo,
            IOrganizationRepository orgRepo,
            IProductProcessRepository processRepo,
            ITransactionRunner tx,
            ILogger<BillingService> logger)
        {
            _userRepo = userRepo;
            _orgRepo = orgRepo;
            _processRepo = processRepo;
            _tx = tx;
            _logger = logger;
        }

        public async Task<GenericDto<GetBalanceResultDto>> GetBalanceAsync(long userId)
        {
            var user = await _userRepo.GetByIdAsync(userId);
            if (user is null)
                return GenericDto<GetBalanceResultDto>.Blocked(StopFactors.User.NotFound);

            return GenericDto<GetBalanceResultDto>.Success(new GetBalanceResultDto
            {
                UserId = userId,
                Balance = ResolveBalance(user)
            });
        }

        public async Task<GenericDto<TopUpBalanceResultDto>> TopUpAsync(TopUpBalanceDto dto)
        {
            var found = await _userRepo.GetByIdAsync(dto.UserId);

            var stop = StopFactorCheck.For(StopActions.BalanceTopUp)
                .StopIf(dto.Amount <= 0, StopFactors.Payment.AmountNotPositive)
                .StopIf(found is null, StopFactors.User.NotFound)
                // Bloklangan foydalanuvchi pulini ishlata olmaydi — hisobni to'ldirish
                // uni "yechib bo'lmaydigan" mablag'ga aylantirardi.
                .StopIf(() => found!.IsBlocked, StopFactors.User.Blocked)
                .StopIf(() => found!.Type == CustomerUserType.Corporate && found.OrganizationId is null,
                        StopFactors.Organization.NotLinked)
                // Organization soft-delete bo'lsa Include null qaytaradi — ikkalasi bir to'siq:
                // tashkilot yo'q yoki nofaol bo'lsa uning balansini to'ldirib bo'lmaydi.
                .StopIf(() => found!.Type == CustomerUserType.Corporate && found.Organization is null,
                        StopFactors.Organization.NotFound)
                .StopIf(() => found!.Type == CustomerUserType.Corporate
                              && found.Organization is { IsActive: false },
                        StopFactors.Organization.Inactive)
                .Result();

            if (stop is not null)
                return GenericDto<TopUpBalanceResultDto>.Blocked(stop);

            var user = found!;

            var newBalance = user.Type == CustomerUserType.Natural
                ? await _userRepo.TopUpBalanceAsync(user.Id, dto.Amount)
                : await _orgRepo.TopUpBalanceAsync(user.OrganizationId!.Value, dto.Amount);

            // Poyga: tekshiruvdan keyin balans egasi o'chirilgan bo'lishi mumkin.
            if (newBalance is null)
                return GenericDto<TopUpBalanceResultDto>.Blocked(
                    user.Type == CustomerUserType.Natural
                        ? StopFactors.User.NotFound
                        : StopFactors.Organization.NotFound);

            _logger.LogInformation(
                "Balans to'ldirildi: userId={UserId} type={Type} amount={Amount} newBalance={NewBalance}",
                dto.UserId, user.Type, dto.Amount, newBalance);

            return GenericDto<TopUpBalanceResultDto>.Success(new TopUpBalanceResultDto
            {
                NewBalance = newBalance.Value,
                ResultMessage = $"{dto.Amount:N0} UZS muvaffaqiyatli to'ldirildi."
            });
        }

        public async Task<decimal> GetAvailableBalanceAsync(long userId)
        {
            var user = await _userRepo.GetByIdAsync(userId);
            return user is null ? 0 : ResolveBalance(user);
        }

        public async Task<decimal> DeductForProcessAsync(long processId)
        {
            // Claim + yechish bitta tranzaksiyada: yechish yiqilsa claim ham rollback bo'ladi
            // (aks holda process "yechilgan" deb belgilanib, pul yechilmay qolardi).
            return await _tx.RunAsync(async () =>
            {
                var process = await _processRepo.GetByIdWithSessionAsync(processId);
                if (process is null)
                    return 0m;

                // Telemetry hot path ExecuteUpdateAsync bilan yangilagan bo'lishi mumkin —
                // tracker'dagi GivenAmount eskirgan bo'lsa freshlaymiz.
                await _processRepo.ReloadAsync(process);

                // Atomic claim — parallel chaqiruvlardan (device finished + watchdog +
                // session close) faqat bittasi yutadi. Double-deduction himoyasi.
                if (!await _processRepo.TryClaimBalanceDeductionAsync(processId))
                    return 0m;

                var cost = process.GivenAmount * process.PricePerUnit;
                if (cost <= 0)
                    return 0m;

                var user = process.Session?.User ?? await _userRepo.GetByIdAsync(process.Session?.UserId ?? 0);
                if (user is null)
                {
                    _logger.LogWarning("Balans yechilmadi — process {ProcessId} foydalanuvchisi topilmadi.", processId);
                    return 0m;
                }

                decimal deducted = 0;

                if (user.Type == CustomerUserType.Natural)
                {
                    deducted = await _userRepo.DeductBalanceAsync(user.Id, cost);
                }
                else if (user.Type == CustomerUserType.Corporate && user.OrganizationId is not null)
                {
                    deducted = await _orgRepo.DeductBalanceAsync(user.OrganizationId.Value, cost);
                }

                if (deducted < cost)
                    _logger.LogWarning(
                        "Balans yetmadi: processId={ProcessId} userId={UserId} cost={Cost} deducted={Deducted}",
                        processId, user.Id, cost, deducted);
                else
                    _logger.LogInformation(
                        "Balans yechildi: processId={ProcessId} userId={UserId} amount={Deducted}",
                        processId, user.Id, deducted);

                return deducted;
            });
        }

        private static decimal ResolveBalance(CustomerUserEntity user) => user.Type switch
        {
            CustomerUserType.Natural => user.Balance,
            CustomerUserType.Corporate => user.Organization?.Balance ?? 0,
            _ => 0
        };
    }
}
