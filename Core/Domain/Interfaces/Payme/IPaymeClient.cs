namespace Domain.Interfaces.Payme
{
    /// <summary>
    /// Payme Receipts API uchun abstraktsiya. Implementation: typed HttpClient (CommonConfiguration).
    /// Hech qachon throw qilmaydi (network/timeout xatolari ham wrapper ichida qaytadi)
    /// — chunki chaqiruvchi har bir natijani PaymentTransactionStep sifatida audit'ga yozadi.
    /// </summary>
    public interface IPaymeClient
    {
        Task<PaymeApiCall<PaymeReceipt>> CreateReceiptAsync(
            long amountTiyin,
            string orderId,
            CancellationToken ct = default);

        Task<PaymeApiCall<PaymeReceipt>> PayReceiptAsync(
            string receiptId,
            string token,
            CancellationToken ct = default);

        Task<PaymeApiCall<PaymeReceipt>> GetReceiptAsync(
            string receiptId,
            CancellationToken ct = default);
    }
}
