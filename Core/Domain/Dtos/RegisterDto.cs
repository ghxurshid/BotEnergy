namespace Domain.Dtos
{
    public class RegisterDto
    {
        public required string PhoneId { get; set; }
        public required string Mail { get; set; }
        public required string PhoneNumber { get; set; }
    }

    public class RegisterResultDto
    {
        public required long UserId { get; set; }
        public required string ResultMessage { get; set; }
    }
}
