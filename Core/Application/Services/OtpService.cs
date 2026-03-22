using Domain.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Application.Services
{
    public class OtpService : IOtpService
    {
        private static Dictionary<string, string> _storage = new();

        public Task<string> GenerateOtpAsync(string phoneNumber)
        {
            var code = new Random().Next(100000, 999999).ToString();

            _storage[phoneNumber] = code;

            Console.WriteLine($"OTP for {phoneNumber}: {code}");

            return Task.FromResult(code);
        }

        public Task<bool> VerifyOtpAsync(string phoneNumber, string code)
        {
            if (_storage.TryGetValue(phoneNumber, out var saved))
            {
                return Task.FromResult(saved == code);
            }

            return Task.FromResult(false);
        }
    }
}
