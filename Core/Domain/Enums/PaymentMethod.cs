namespace Domain.Enums
{
    /// <summary>
    /// Sessiya uchun to'lov usuli (strategiya). Merchant o'z sozlamasida istalgan paytda
    /// almashtiradi — o'zgarish KEYINGI sessiyalarga ta'sir qiladi; ochiq sessiyaning usuli
    /// <see cref="Domain.Entities.PaymentSessionEntity.Method"/> da qotirilgan bo'lib qoladi
    /// (FIFO consume va yakuniy hisob-kitob bir xil semantikada tugashi uchun).
    ///
    /// Raqamli qiymatlar DB'da saqlanadi — o'zgartirilmaydi.
    /// </summary>
    public enum PaymentMethod
    {
        /// <summary>
        /// Payme Merchant API: mijoz checkout link/QR orqali to'laydi, Payme BIZGA callback qiladi.
        /// Pul darhol yechiladi (hold yo'q).
        /// </summary>
        Merchant = 0,

        /// <summary>
        /// Payme Receipts API: server chek yaratadi va mijozga yetkazadi (SMS/deep-link),
        /// mijoz Payme ilovasida to'laydi. Pul darhol yechiladi (hold yo'q).
        /// </summary>
        Invoice = 1,

        /// <summary>
        /// Payme Subscribe API: saqlangan karta tokeni bilan server to'laydi.
        /// YAGONA usul — hold (pre-authorization) shu yerda mavjud.
        /// </summary>
        Subscribe = 2
    }

    /// <summary>
    /// Merchant ruxsat bergan usullar to'plami (bitmask ustun).
    /// <see cref="PaymentMethod"/> qiymatlariga <c>PaymentMethodFlagsExtensions.ToFlag</c> orqali mos keladi.
    /// </summary>
    [Flags]
    public enum PaymentMethodFlags
    {
        None = 0,
        Merchant = 1 << 0,
        Invoice = 1 << 1,
        Subscribe = 1 << 2
    }

    public static class PaymentMethodFlagsExtensions
    {
        public static PaymentMethodFlags ToFlag(this PaymentMethod method) => method switch
        {
            PaymentMethod.Merchant => PaymentMethodFlags.Merchant,
            PaymentMethod.Invoice => PaymentMethodFlags.Invoice,
            PaymentMethod.Subscribe => PaymentMethodFlags.Subscribe,
            _ => PaymentMethodFlags.None
        };

        public static bool Allows(this PaymentMethodFlags flags, PaymentMethod method)
            => flags.HasFlag(method.ToFlag());

        /// <summary>Bitmask'dagi usullarni ro'yxat qilib qaytaradi (UI uchun).</summary>
        public static IReadOnlyList<PaymentMethod> ToMethods(this PaymentMethodFlags flags)
            => Enum.GetValues<PaymentMethod>().Where(m => flags.Allows(m)).ToList();
    }
}
