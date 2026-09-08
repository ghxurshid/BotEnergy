namespace Domain.Payments
{
    /// <summary>
    /// Payme Merchant API kirish nuqtasi: Payme BIZGA JSON-RPC qiladi
    /// (CheckPerformTransaction / CreateTransaction / PerformTransaction /
    /// CancelTransaction / CheckTransaction / GetStatement).
    ///
    /// Har bir merchant Payme kabinetida O'Z callback URL'ini ko'rsatadi
    /// (<c>/api/PaymeMerchant/Callback/{merchantId}</c>), shuning uchun autentifikatsiya
    /// aynan o'sha merchantning kaliti bilan tekshiriladi.
    ///
    /// SessionApi-only: to'lov tasdiqlangach sessiya balansi va real-time eventlar shu jarayonda yangilanadi.
    /// </summary>
    public interface IPaymeMerchantGateway
    {
        /// <summary>
        /// Callback'ni bajaradi. HECH QACHON throw qilmaydi — barcha nosozlik JSON-RPC
        /// xatosi sifatida qaytadi (Payme HTTP 200 + error kutadi).
        /// </summary>
        /// <param name="merchantId">URL'dagi merchant (kalit shu merchantniki bilan solishtiriladi).</param>
        /// <param name="authorizationHeader">"Basic base64(Paycom:&lt;key&gt;)" — bo'lmasa -32504.</param>
        Task<PaymeRpcResponse> HandleAsync(
            long merchantId,
            string? authorizationHeader,
            PaymeRpcRequest request,
            CancellationToken ct = default);
    }
}
