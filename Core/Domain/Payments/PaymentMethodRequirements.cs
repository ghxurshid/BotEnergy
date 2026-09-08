using Domain.Entities;
using Domain.Enums;

namespace Domain.Payments
{
    /// <summary>
    /// Har bir to'lov usuli merchantdan nimani talab qilishi. Merchant usulni RUNTIME'da
    /// almashtirgani uchun bu tekshiruv sozlama saqlanishidan OLDIN bajariladi —
    /// aks holda merchant o'zini "to'lov qabul qila olmaydigan" holatga qo'yib qo'yardi.
    ///
    /// Ataylab strategiya ro'yxatiga bog'lanmagan: MerchantService barcha API'larda
    /// ro'yxatga olinadi, strategiyalar esa faqat SessionApi'da.
    /// </summary>
    public static class PaymentMethodRequirements
    {
        /// <summary>Merchant shu usul bilan ishlashga tayyormi (credential'lar to'liqmi).</summary>
        public static bool IsConfigured(MerchantEntity merchant, PaymentMethod method) => method switch
        {
            // Receipts/Subscribe API — kassa credential'lari.
            PaymentMethod.Subscribe or PaymentMethod.Invoice =>
                merchant.PaymeEnabled
                && !string.IsNullOrWhiteSpace(merchant.PaymeCashboxId)
                && !string.IsNullOrWhiteSpace(merchant.PaymeKey),

            // Merchant API — checkout id + callback kaliti.
            PaymentMethod.Merchant =>
                !string.IsNullOrWhiteSpace(merchant.PaymeMerchantId)
                && !string.IsNullOrWhiteSpace(merchant.PaymeMerchantKey),

            _ => false
        };

        /// <summary>Sozlanmagan usul uchun operatorga aytiladigan sabab.</summary>
        public static string MissingRequirement(PaymentMethod method) => method switch
        {
            PaymentMethod.Subscribe or PaymentMethod.Invoice =>
                "Payme kassasi sozlanmagan (CashboxId + Key, PaymeEnabled=true).",
            PaymentMethod.Merchant =>
                "Payme Merchant API sozlanmagan (MerchantId + Key).",
            _ => "Usul qo'llab-quvvatlanmaydi."
        };
    }
}
