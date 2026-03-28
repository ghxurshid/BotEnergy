namespace Domain.Dtos
{
    public class GetUserDto
    {
        public long Id { get; set; }
        public string PhoneId { get; set; } = string.Empty;
        public string Mail { get; set; } = string.Empty;
        public string PhoneNumber { get; set; } = string.Empty;
        public decimal Balance { get; set; }
        public bool IsVerified { get; set; }
        public bool IsBlocked { get; set; }
        public DateTime LastLoginDate { get; set; }
        public DateTime LastActiveDate { get; set; }
        public DateTime CreatedDate { get; set; }
    }

    public class UpdateUserDto
    {
        public string? Mail { get; set; }
        public string? PhoneId { get; set; }
    }

    public class UpdateUserResultDto
    {
        public required string ResultMessage { get; set; }
    }
}
