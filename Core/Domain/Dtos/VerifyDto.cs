namespace Domain.Dtos
{
    public class VerifyDto
    {
        public required string PhoneNumber { get; set; }
        public required string OtpCode { get; set; }
    }

    public class VerifyResultDto
    {
        public required string ResultMessage { get; set; }
    }
}
