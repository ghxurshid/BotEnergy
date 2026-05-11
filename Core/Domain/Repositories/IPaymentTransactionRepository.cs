using Domain.Entities;
using Domain.Enums;

namespace Domain.Repositories
{
    public interface IPaymentTransactionRepository
    {
        Task<PaymentTransactionEntity> CreateAsync(PaymentTransactionEntity transaction);

        Task<PaymentTransactionEntity?> GetByIdAsync(long id, bool includeSteps = false);

        Task<PaymentTransactionEntity?> GetByProviderOrderIdAsync(string providerOrderId);

        Task<PaymentTransactionEntity?> GetByIdempotencyKeyAsync(string idempotencyKey);

        Task UpdateAsync(PaymentTransactionEntity transaction);

        /// <summary>
        /// Append-only — yangi qadam qo'shadi, hech qachon update qilmaydi.
        /// </summary>
        Task AddStepAsync(PaymentTransactionStepEntity step);

        Task<IReadOnlyList<PaymentTransactionEntity>> ListForUserAsync(
            long userId,
            int skip,
            int take,
            PaymentStatus? status = null);

        Task<IReadOnlyList<PaymentTransactionEntity>> ListForOrganizationAsync(
            long organizationId,
            int skip,
            int take,
            PaymentStatus? status = null);

        /// <summary>Admin audit uchun barcha tranzaksiyalar — filter va paginatsiya bilan.</summary>
        Task<IReadOnlyList<PaymentTransactionEntity>> ListAllAsync(
            int skip,
            int take,
            PaymentStatus? status = null,
            DateTime? from = null,
            DateTime? to = null);

        Task<IReadOnlyList<PaymentTransactionStepEntity>> GetStepsAsync(long transactionId);
    }
}
