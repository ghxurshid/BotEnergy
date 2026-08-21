using System.Text.RegularExpressions;

namespace Domain.Helpers
{
    /// <summary>
    /// Karta raqami (PAN) bilan ishlashning yagona joyi.
    ///
    /// <b>To'liq PAN hech qayerda saqlanmaydi va loglanmaydi</b> — u faqat bank tekshiruvi
    /// chaqiruvi davomida xotirada bo'ladi. Bazaga va logga faqat <see cref="Mask"/>
    /// natijasi tushadi.
    /// </summary>
    public static class CardNumberHelper
    {
        public const string ErrorMessage = "Karta raqami noto'g'ri. 16 xonali raqam kiriting.";

        private static readonly Regex DigitsOnly = new(@"^\d{16}$", RegexOptions.Compiled);

        /// <summary>Bo'shliq va tirelarni olib tashlaydi.</summary>
        public static string Normalize(string? pan)
            => string.IsNullOrWhiteSpace(pan)
                ? string.Empty
                : new string(pan.Where(char.IsDigit).ToArray());

        /// <summary>16 xonalik va Luhn nazorat summasi tekshiruvi.</summary>
        public static bool IsValid(string? pan)
        {
            var normalized = Normalize(pan);
            return DigitsOnly.IsMatch(normalized) && PassesLuhn(normalized);
        }

        public static bool TryNormalize(string? pan, out string normalized)
        {
            normalized = Normalize(pan);
            return IsValid(normalized);
        }

        /// <summary>
        /// <c>8600123412341234</c> → <c>8600****1234</c>.
        /// Yaroqsiz uzunlikda ham xavfsiz ishlaydi — hech qachon to'liq raqam qaytarmaydi.
        /// </summary>
        public static string Mask(string? pan)
        {
            var normalized = Normalize(pan);
            if (normalized.Length < 10)
                return "****";

            return $"{normalized[..4]}****{normalized[^4..]}";
        }

        /// <summary>Luhn algoritmi — terish xatolarining katta qismini bank chaqiruvisiz ushlaydi.</summary>
        private static bool PassesLuhn(string digits)
        {
            var sum = 0;
            var alternate = false;

            for (var i = digits.Length - 1; i >= 0; i--)
            {
                var n = digits[i] - '0';

                if (alternate)
                {
                    n *= 2;
                    if (n > 9) n -= 9;
                }

                sum += n;
                alternate = !alternate;
            }

            return sum % 10 == 0;
        }
    }
}
