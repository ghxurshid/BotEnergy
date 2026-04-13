using System.Text.RegularExpressions;

namespace CommonConfiguration.Validators
{
    /// <summary>
    /// Telefon raqam formati: 998 bilan boshlanadi, jami 12 raqam, + belgisisiz.
    /// Misol: 998901234567
    /// </summary>
    public static class PhoneValidator
    {
        private static readonly Regex _regex = new(@"^998[0-9]{9}$", RegexOptions.Compiled);

        public static bool IsValid(string? phone)
            => !string.IsNullOrEmpty(phone) && _regex.IsMatch(phone);

        public static string ErrorMessage => "Telefon raqam 998 bilan boshlanishi va 12 ta raqamdan iborat bo'lishi kerak (masalan: 998901234567).";
    }
}
