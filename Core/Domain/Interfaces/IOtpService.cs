using Domain.Enums;

namespace Domain.Interfaces
{
    public interface IOtpService
    {
        Task<string> GenerateOtpAsync(string phoneNumber, OtpPurpose purpose);
        Task<bool> VerifyOtpAsync(string phoneNumber, string code, OtpPurpose purpose);
        Task<bool> IsOtpVerifiedAsync(string phoneNumber, OtpPurpose purpose);
        Task ConsumeOtpVerificationAsync(string phoneNumber, OtpPurpose purpose);
    }
}
