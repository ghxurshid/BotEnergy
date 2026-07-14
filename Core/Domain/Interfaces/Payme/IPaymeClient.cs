namespace Domain.Interfaces.Payme
{
    /// <summary>
    /// Payme Receipts API uchun abstraktsiya. Implementation: typed HttpClient (CommonConfiguration).
    /// Hech qachon throw qilmaydi (network/timeout xatolari ham wrapper ichida qaytadi)
    /// — chunki chaqiruvchi har bir natijani audit step sifatida yozadi.
    /// <paramref name="creds"/> null bo'lsa global PaymeOptions (default kassa) ishlatiladi;
    /// hold invoice oqimida har doim device egasi merchant'ning credential'lari beriladi.
    /// </summary>
    public interface IPaymeClient
    {
        /// <summary>receipts.create. <paramref name="hold"/>=true — pre-authorization (pul darrov yechilmaydi).</summary>
        Task<PaymeApiCall<PaymeReceipt>> CreateReceiptAsync(
            long amountTiyin,
            string orderId,
            bool hold = false,
            string? description = null,
            PaymeCredentials? creds = null,
            CancellationToken ct = default);

        /// <summary>receipts.pay — karta tokeni bilan to'lash (mavjud QR top-up oqimi).</summary>
        Task<PaymeApiCall<PaymeReceipt>> PayReceiptAsync(
            string receiptId,
            string token,
            PaymeCredentials? creds = null,
            CancellationToken ct = default);

        Task<PaymeApiCall<PaymeReceipt>> GetReceiptAsync(
            string receiptId,
            PaymeCredentials? creds = null,
            CancellationToken ct = default);

        /// <summary>receipts.send — chekni mijoz telefoniga (SMS invoice) yuborish.</summary>
        Task<PaymeApiCall<PaymeReceipt>> SendReceiptAsync(
            string receiptId,
            string phone,
            PaymeCredentials? creds = null,
            CancellationToken ct = default);

        /// <summary>receipts.check — holat polling (javobda faqat state).</summary>
        Task<PaymeApiCall<PaymeReceipt>> CheckReceiptAsync(
            string receiptId,
            PaymeCredentials? creds = null,
            CancellationToken ct = default);

        /// <summary>
        /// receipts.confirm_hold — ushlangan pulni yechish.
        /// <paramref name="amountTiyin"/> berilsa qisman capture (qolgani avtomatik bo'shaydi),
        /// null — to'liq summa.
        /// </summary>
        Task<PaymeApiCall<PaymeReceipt>> ConfirmHoldAsync(
            string receiptId,
            long? amountTiyin,
            PaymeCredentials? creds = null,
            CancellationToken ct = default);

        /// <summary>receipts.cancel — chekni bekor qilish / hold'ni qaytarish.</summary>
        Task<PaymeApiCall<PaymeReceipt>> CancelReceiptAsync(
            string receiptId,
            PaymeCredentials? creds = null,
            CancellationToken ct = default);
    }
}
