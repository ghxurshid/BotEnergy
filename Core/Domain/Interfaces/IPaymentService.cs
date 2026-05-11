using Domain.Dtos.Base;
using Domain.Dtos.Payment;

namespace Domain.Interfaces
{
    /// <summary>
    /// QR orqali tashqi to'lov tizimi (Payme) bilan balans to'ldirish.
    /// Har bir qadam <see cref="Domain.Entities.PaymentTransactionStepEntity"/> sifatida audit'ga yoziladi.
    /// </summary>
    public interface IPaymentService
    {
        Task<GenericDto<QrTopUpResultDto>> ProcessQrTopUpAsync(QrTopUpRequestDto request, CancellationToken ct = default);

        /// <summary>
        /// Admin operatsiyasi: muvaffaqiyatli to'lovni qo'lda bekor qilish (balansdan ayirish, status=Reversed).
        /// Faqat Status=Succeeded bo'lgan tranzaksiyalar uchun. Step audit'iga yoziladi.
        /// </summary>
        Task<GenericDto<ReverseTransactionResultDto>> ReverseAsync(long transactionId, long performedByUserId, string reason);
    }
}
