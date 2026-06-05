using Domain.Dtos;
using Domain.Dtos.Base;
using Domain.Entities;
using Domain.Enums;
using Domain.Interfaces;
using Domain.Repositories;

namespace Application.Services
{
    public class BillingService : IBillingService
    {
        private readonly ICustomerUserRepository _userRepo;
        private readonly IProductProcessRepository _processRepo;

        public BillingService(ICustomerUserRepository userRepo, IProductProcessRepository processRepo)
        {
            _userRepo = userRepo;
            _processRepo = processRepo;
        }

        public async Task<GenericDto<GetBalanceResultDto>> GetBalanceAsync(long userId)
        {
            var user = await _userRepo.GetByIdAsync(userId);
            if (user is null)
                return GenericDto<GetBalanceResultDto>.Error(404, "Foydalanuvchi topilmadi.");

            return GenericDto<GetBalanceResultDto>.Success(new GetBalanceResultDto
            {
                UserId = userId,
                Balance = ResolveBalance(user)
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

            if (user.Type == CustomerUserType.Natural)
            {
                user.Balance += dto.Amount;
                newBalance = user.Balance;
            }
            else // Corporate
            {
                if (user.Organization is null)
                    return GenericDto<TopUpBalanceResultDto>.Error(400, "Corporate foydalanuvchining tashkiloti topilmadi.");

                user.Organization.Balance += dto.Amount;
                newBalance = user.Organization.Balance;
            }

            await _userRepo.UpdateAsync(user);

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
            if (process is null)
                return 0;

            // ExecuteUpdateAsync orqali ushbu scope ichida row allaqachon yangilangan bo'lishi mumkin
            // (telemetry hot path), shuning uchun freshlatib olamiz.
            await _processRepo.ReloadAsync(process);

            if (process.IsBalanceDeducted)
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

            if (user.Type == CustomerUserType.Natural)
            {
                deducted = Math.Min(user.Balance, cost);
                user.Balance -= deducted;
                await _userRepo.UpdateAsync(user);
            }
            else if (user.Type == CustomerUserType.Corporate && user.Organization is not null)
            {
                deducted = Math.Min(user.Organization.Balance, cost);
                user.Organization.Balance -= deducted;
                await _userRepo.UpdateAsync(user);
            }

            process.IsBalanceDeducted = true;
            await _processRepo.UpdateAsync(process);

            return deducted;
        }

        private static decimal ResolveBalance(CustomerUserEntity user) => user.Type switch
        {
            CustomerUserType.Natural => user.Balance,
            CustomerUserType.Corporate => user.Organization?.Balance ?? 0,
            _ => 0
        };
    }
}
