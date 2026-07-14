using Domain.Helpers;

namespace CommonConfiguration.Validators
{
    /// <summary>
    /// Telefon raqam formati: 998 bilan boshlanadi, jami 12 raqam, + belgisisiz.
    /// Misol: 998901234567.
    /// Yagona (canonical) logika <see cref="PhoneNumberHelper"/> da (Domain) — API filtrlar ham,
    /// DB SaveChanges guard ham bir xil qoidadan foydalanadi.
    /// </summary>
    public static class PhoneValidator
    {
        public static bool IsValid(string? phone) => PhoneNumberHelper.IsValid(phone);

        /// <summary>Yumshoq normalizatsiya (bo'sh joy/`-`/`()`/`+`/`00` prefiks tozalanadi). Format kafolatlanmaydi.</summary>
        public static string? Normalize(string? phone) => PhoneNumberHelper.Normalize(phone);

        /// <summary>Normalizatsiya qilib, canonical formatga to'g'ri kelsa true. <paramref name="normalized"/> — tozalangan qiymat.</summary>
        public static bool TryNormalize(string? phone, out string normalized) => PhoneNumberHelper.TryNormalize(phone, out normalized);

        public static string ErrorMessage => PhoneNumberHelper.ErrorMessage;
    }
}
