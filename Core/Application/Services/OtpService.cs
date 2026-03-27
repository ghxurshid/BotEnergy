using Domain.Enums;
using Domain.Interfaces;

namespace Application.Services
{
    public class OtpService : IOtpService
    {
        private static readonly Dictionary<string, string> _otpStorage = new();
        private static readonly HashSet<string> _verifiedStorage = new();

        private static string Key(string phoneNumber, OtpPurpose purpose)
            => $"{phoneNumber}:{purpose}";

        public Task<string> GenerateOtpAsync(string phoneNumber, OtpPurpose purpose)
        {
            var code = new Random().Next(100000, 999999).ToString();
            var key = Key(phoneNumber, purpose);

            _otpStorage[key] = code;
            _verifiedStorage.Remove(key);

            Console.WriteLine($"OTP for {phoneNumber} [{purpose}]: {code}");

            return Task.FromResult(code);
        }

        public Task<bool> VerifyOtpAsync(string phoneNumber, string code, OtpPurpose purpose)
        {
            var key = Key(phoneNumber, purpose);

            if (_otpStorage.TryGetValue(key, out var saved) && saved == code)
            {
                _verifiedStorage.Add(key);
                _otpStorage.Remove(key);
                return Task.FromResult(true);
            }

            return Task.FromResult(false);
        }

        public Task<bool> IsOtpVerifiedAsync(string phoneNumber, OtpPurpose purpose)
            => Task.FromResult(_verifiedStorage.Contains(Key(phoneNumber, purpose)));

        public Task ConsumeOtpVerificationAsync(string phoneNumber, OtpPurpose purpose)
        {
            _verifiedStorage.Remove(Key(phoneNumber, purpose));
            return Task.CompletedTask;
        }
    }
}
