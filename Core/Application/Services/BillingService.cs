using Domain.Dtos;
using Domain.Dtos.Base;
using Domain.Entities;
using Domain.Interfaces;
using Domain.Repositories;

namespace Application.Services
{
    public class BillingService : IBillingService
    {
        private readonly IUserRepository _userRepo;
        private readonly IProductProcessRepository _processRepo;

        public BillingService(IUserRepository userRepo, IProductProcessRepository processRepo)
        {
            _userRepo = userRepo;
            _processRepo = processRepo;
        }

        public async Task<GenericDto<GetBalanceResultDto>> GetBalanceAsync(long userId)
        {
            var user = await _userRepo.GetByIdAsync(userId);
            if (user is null)
                return GenericDto<GetBalanceResultDto>.Error(404, "Foydalanuvchi topilmadi.");

            decimal balance = ResolveBalance(user);

            return GenericDto<GetBalanceResultDto>.Success(new GetBalanceResultDto
            {
                UserId = userId,
                Balance = balance
            });
        }

        public async Task<GenericDto<TopUpBalanceResultDto>> TopUpAsync(TopUpBalanceDto dto)
        {
            if (dto.Amount <= 0)
                return GenericDto<TopUpBalanceResultDto>.Error(400, "To'ldirish miqdori 0 dan katta bo'lishi kerak.");

            var user = await _userRepo.GetByIdAsync(dto.UserId);
            if (user is null)
                return GenericDto<TopUpBalanceResultDto>.Error(404, "Foydalanuvchi topilmadi.");

            decimal newBalance;

            if (user is NaturalUserEntity natural)
            {
                natural.Balance += dto.Amount;
                newBalance = natural.Balance;
            }
            else if (user is LegalUserEntity legal)
            {
                if (legal.Organization is null)
                    return GenericDto<TopUpBalanceResultDto>.Error(400, "Yuridik foydalanuvchining tashkiloti topilmadi.");

                legal.Organization.Balance += dto.Amount;
                newBalance = legal.Organization.Balance;
            }
            else
            {
                return GenericDto<TopUpBalanceResultDto>.Error(400, "Foydalanuvchi turi aniqlanmadi.");
            }

            await _userRepo.UpdateUserAsync(user);

            return GenericDto<TopUpBalanceResultDto>.Success(new TopUpBalanceResultDto
            {
                NewBalance = newBalance,
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
            var process = await _processRepo.GetByIdWithSessionAsync(processId);
            if (process is null || process.IsBalanceDeducted)
                return 0;

            var cost = process.GivenAmount * process.PricePerUnit;
            if (cost <= 0)
            {
                process.IsBalanceDeducted = true;
                await _processRepo.UpdateAsync(process);
                return 0;
            }

            var user = process.Session?.User ?? await _userRepo.GetByIdAsync(process.Session?.UserId ?? 0);
            if (user is null)
                return 0;

            decimal deducted = 0;

            if (user is NaturalUserEntity natural)
            {
                deducted = Math.Min(natural.Balance, cost);
                natural.Balance -= deducted;
                await _userRepo.UpdateUserAsync(natural);
            }
            else if (user is LegalUserEntity legal && legal.Organization is not null)
            {
                deducted = Math.Min(legal.Organization.Balance, cost);
                legal.Organization.Balance -= deducted;
                await _userRepo.UpdateUserAsync(legal);
            }

            process.IsBalanceDeducted = true;
            await _processRepo.UpdateAsync(process);

            return deducted;
        }

        private static decimal ResolveBalance(UserEntity user) => user switch
        {
            NaturalUserEntity natural => natural.Balance,
            LegalUserEntity legal => legal.Organization?.Balance ?? 0,
            _ => 0
        };
    }
}
