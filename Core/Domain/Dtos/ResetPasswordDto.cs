namespace Domain.Dtos
{
    public class ResetPasswordRequestDto
    {
        public required string PhoneNumber { get; set; }
    }

    public class ResetPasswordRequestResultDto
    {
        public required long UserId { get; set; }
        public required string ResultMessage { get; set; }
    }

    public class ResetPasswordVerifyDto
    {
        public required long UserId { get; set; }
        public required string OtpCode { get; set; }
    }

    public class ResetPasswordVerifyResultDto
    {
        public required string ResultMessage { get; set; }
    }

    public class ResetPasswordSetDto
    {
        public required long UserId { get; set; }
        public required string NewPassword { get; set; }
    }

    public class ResetPasswordSetResultDto
    {
        public required string ResultMessage { get; set; }
    }
}
