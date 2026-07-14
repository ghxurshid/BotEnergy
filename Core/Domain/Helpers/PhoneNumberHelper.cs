using System.Text;
using System.Text.RegularExpressions;

namespace Domain.Helpers
{
    /// <summary>
    /// Telefon raqamlar uchun yagona (canonical) manba: normalizatsiya + format tekshiruvi.
    /// Kafolatlangan format: <c>998</c> bilan boshlanadi, jami 12 ta raqam, + belgisisiz.
    /// Misol: <c>998901234567</c>.
    ///
    /// Bu Domain'da turadi — ham API validatsiya filtrlar (CommonConfiguration.PhoneValidator),
    /// ham DB yozuv choke-point'i (AppDbContext.SaveChanges guard) shu yerdan foydalanadi.
    /// </summary>
    public static class PhoneNumberHelper
    {
        private static readonly Regex CanonicalRegex = new(@"^998[0-9]{9}$", RegexOptions.Compiled);

        public const string ErrorMessage =
            "Telefon raqam 998 bilan boshlanishi va 12 ta raqamdan iborat bo'lishi kerak (masalan: 998901234567).";

        /// <summary>Aynan <c>998XXXXXXXXX</c> formatidami (normalizatsiyasiz, qat'iy tekshiruv).</summary>
        public static bool IsValid(string? phone)
            => !string.IsNullOrEmpty(phone) && CanonicalRegex.IsMatch(phone);

        /// <summary>
        /// Yumshoq normalizatsiya: bo'sh joy, <c>-</c>, <c>(</c> <c>)</c>, <c>.</c> tozalanadi;
        /// bir dona bosh <c>+</c> yoki xalqaro <c>00</c> prefiks olib tashlanadi.
        /// Faqat tozalaydi — format haqiqiyligini kafolatlamaydi (buni <see cref="IsValid"/> tekshiradi).
        /// Mahalliy 9 xonali raqamga <c>998</c> avtomatik qo'shilmaydi (noaniqlik xavfi).
        /// </summary>
        public static string? Normalize(string? phone)
        {
            if (string.IsNullOrWhiteSpace(phone))
                return phone;

            var sb = new StringBuilder(phone.Length);
            foreach (var ch in phone)
            {
                if (char.IsWhiteSpace(ch) || ch == '-' || ch == '(' || ch == ')' || ch == '.')
                    continue;
                sb.Append(ch);
            }

            var s = sb.ToString();

            // Bosh xalqaro prefikslar: "+998..." yoki "00998..." → "998..."
            if (s.StartsWith('+'))
                s = s[1..];
            if (s.StartsWith("00"))
                s = s[2..];

            return s;
        }

        /// <summary>
        /// Normalizatsiya qilib, natija canonical formatga to'g'ri kelsa <c>true</c> qaytaradi.
        /// <paramref name="normalized"/> — tozalangan qiymat (yaroqsiz bo'lsa ham qaytadi, xabar uchun).
        /// </summary>
        public static bool TryNormalize(string? phone, out string normalized)
        {
            normalized = Normalize(phone) ?? string.Empty;
            return IsValid(normalized);
        }
    }
}
