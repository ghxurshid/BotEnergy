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

        public BillingService(IUserRepository userRepo)
            => _userRepo = userRepo;

        public async Task<GenericDto<GetBalanceResultDto>> GetBalanceAsync(long userId)
        {
            var user = await _userRepo.GetByIdAsync(userId);
            if (user is null)
                return GenericDto<GetBalanceResultDto>.Error(404, "Foydalanuvchi topilmadi.");

            decimal balance = user switch
            {
                NaturalUserEntity natural => natural.Balance,
                LegalUserEntity legal => legal.Organization?.Balance ?? 0,
                _ => 0
            };

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
    }
}
