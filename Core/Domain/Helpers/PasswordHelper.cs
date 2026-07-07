using System.Security.Cryptography;
using System.Text;

namespace Domain.Helpers
{
    /// <summary>
    /// Parol hash: PBKDF2-HMAC-SHA256 (OWASP tavsiyasi bo'yicha 600k iteratsiya).
    ///
    /// Format (PasswordHash ustunida): <c>PBKDF2$&lt;iterations&gt;$&lt;base64 hash&gt;</c>,
    /// PasswordSalt ustunida — base64 salt (16 bayt).
    ///
    /// Legacy format (marker'siz): SHA256(password + salt) base64, salt — GUID string.
    /// <see cref="Verify"/> ikkala formatni ham qabul qiladi; <see cref="NeedsRehash"/> true
    /// qaytarsa login oqimi parolni yangi formatda qayta saqlashi kerak (lazy re-hash).
    /// </summary>
    public static class PasswordHelper
    {
        private const string Pbkdf2Marker = "PBKDF2";
        private const int Iterations = 600_000;
        private const int SaltSize = 16;
        private const int HashSize = 32;

        public static (string hash, string salt) CreatePassword(string password)
        {
            var saltBytes = RandomNumberGenerator.GetBytes(SaltSize);
            var hashBytes = Rfc2898DeriveBytes.Pbkdf2(
                password, saltBytes, Iterations, HashAlgorithmName.SHA256, HashSize);

            var hash = $"{Pbkdf2Marker}${Iterations}${Convert.ToBase64String(hashBytes)}";
            return (hash, Convert.ToBase64String(saltBytes));
        }

        public static bool Verify(string password, string hash, string salt)
        {
            if (hash.StartsWith(Pbkdf2Marker + "$", StringComparison.Ordinal))
                return VerifyPbkdf2(password, hash, salt);

            return VerifyLegacySha256(password, hash, salt);
        }

        /// <summary>Legacy (SHA256) hash bo'lsa true — login'da qayta hash'lash kerak.</summary>
        public static bool NeedsRehash(string hash)
            => !hash.StartsWith(Pbkdf2Marker + "$", StringComparison.Ordinal);

        private static bool VerifyPbkdf2(string password, string hash, string salt)
        {
            var parts = hash.Split('$');
            if (parts.Length != 3 || !int.TryParse(parts[1], out var iterations))
                return false;

            byte[] saltBytes;
            byte[] expected;
            try
            {
                saltBytes = Convert.FromBase64String(salt);
                expected = Convert.FromBase64String(parts[2]);
            }
            catch (FormatException)
            {
                return false;
            }

            var actual = Rfc2898DeriveBytes.Pbkdf2(
                password, saltBytes, iterations, HashAlgorithmName.SHA256, expected.Length);

            return CryptographicOperations.FixedTimeEquals(actual, expected);
        }

        private static bool VerifyLegacySha256(string password, string hash, string salt)
        {
            byte[] expected;
            try
            {
                expected = Convert.FromBase64String(hash);
            }
            catch (FormatException)
            {
                return false;
            }

            var actual = SHA256.HashData(Encoding.UTF8.GetBytes(password + salt));
            return CryptographicOperations.FixedTimeEquals(actual, expected);
        }
    }
}
