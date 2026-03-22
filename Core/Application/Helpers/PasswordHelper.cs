using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Application.Helpers
{
    public static class PasswordHelper
    {
        public static (string hash, string salt) CreatePassword(string password)
        {
            var salt = Guid.NewGuid().ToString();

            var hash = Convert.ToBase64String(
                System.Security.Cryptography.SHA256.HashData(
                    Encoding.UTF8.GetBytes(password + salt)));

            return (hash, salt);
        }

        public static bool Verify(string password, string hash, string salt)
        {
            var newHash = Convert.ToBase64String(
                System.Security.Cryptography.SHA256.HashData(
                    Encoding.UTF8.GetBytes(password + salt)));

            return newHash == hash;
        }
    }
}
