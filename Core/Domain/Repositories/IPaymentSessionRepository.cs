using Domain.Entities;
using Domain.Enums;

namespace Domain.Repositories
{
    public interface IPaymentSessionRepository
    {
        Task<PaymentSessionEntity> CreateAsync(PaymentSessionEntity paymentSession);

        Task<PaymentSessionEntity?> GetByIdAsync(long id);

        /// <summary>Sessiyaga bog'langan payment session (1:1).</summary>
        Task<PaymentSessionEntity?> GetBySessionIdAsync(long sessionId, bool includeInvoices = false);

        /// <summary>
        /// Atomik status o'tishi: joriy status <paramref name="from"/> ro'yxatida bo'lsagina yozadi.
        /// </summary>
        Task<bool> TryTransitionAsync(long id, PaymentSessionStatus to, params PaymentSessionStatus[] from);

        /// <summary>Hold balansini atomik oshiradi (invoice Hold'ga o'tganda).</summary>
        Task TryAddFundedBalanceAsync(long id, long deltaTiyin);

        /// <summary>
        /// Consumed'ni atomik oshiradi — faqat yetarli available (hold - consumed) bo'lsa.
        /// False — mablag' yetmadi.
        /// </summary>
        Task<bool> TryConsumeBalanceAsync(long id, long deltaTiyin);

        /// <summary>Settling holatidagi payment session'lar (watcher finalize uchun).</summary>
        Task<List<PaymentSessionEntity>> GetSettlingAsync(int take);

        /// <summary>
        /// UpdatedDate'ni yangilaydi (entity tracking'siz). Watcher navbati UpdatedDate bo'yicha
        /// saralanadi — operator hal qilishini kutayotgan sessiya navbat boshini band qilib,
        /// yangilarini yakunlanmay qoldirmasligi uchun ishlatiladi.
        /// </summary>
        Task TouchAsync(long id);

        Task UpdateAsync(PaymentSessionEntity paymentSession);
    }
}
