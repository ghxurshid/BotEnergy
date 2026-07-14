using Domain.Interfaces.Payme;
using Domain.Repositories;

namespace Application.Services
{
    public class PaymeCredentialResolver : IPaymeCredentialResolver
    {
        private readonly IMerchantRepository _merchantRepo;

        public PaymeCredentialResolver(IMerchantRepository merchantRepo)
            => _merchantRepo = merchantRepo;

        public async Task<PaymeCredentials?> ForMerchantAsync(long merchantId)
        {
            var merchant = await _merchantRepo.GetByIdAsync(merchantId);
            if (merchant is null || !merchant.IsActive || !merchant.PaymeEnabled)
                return null;

            if (string.IsNullOrWhiteSpace(merchant.PaymeCashboxId) || string.IsNullOrWhiteSpace(merchant.PaymeKey))
                return null;

            return new PaymeCredentials(merchant.PaymeCashboxId, merchant.PaymeKey);
        }
    }
}
