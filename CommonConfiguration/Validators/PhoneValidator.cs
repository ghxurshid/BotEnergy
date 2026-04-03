using System.Text.RegularExpressions;

namespace CommonConfiguration.Validators
{
    /// <summary>
    /// Telefon raqam formati: probelsiz, + belgisisiz, faqat 10 raqam.
    /// Misol: 9012345678
    /// </summary>
    public static class PhoneValidator
    {
        private static readonly Regex _regex = new(@"^[0-9]{10}$", RegexOptions.Compiled);

        public static bool IsValid(string? phone)
            => !string.IsNullOrEmpty(phone) && _regex.IsMatch(phone);

        public static string ErrorMessage => "Telefon raqam faqat 10 ta raqamdan iborat bo'lishi kerak (masalan: 9012345678).";
    }
}
