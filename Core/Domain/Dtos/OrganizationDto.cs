namespace Domain.Dtos
{
    public class CreateOrganizationDto
    {
        public required string Name { get; set; }
        public required string Inn { get; set; }
        public required string Address { get; set; }
        public required string PhoneNumber { get; set; }
        public decimal Balance { get; set; } = 0;
        public bool IsActive { get; set; } = true;
    }

    public class UpdateOrganizationDto
    {
        public string? Address { get; set; }
        public string? PhoneNumber { get; set; }
        public bool? IsActive { get; set; }
    }

    public class OrganizationItemDto
    {
        public long Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Inn { get; set; } = string.Empty;
        public string Address { get; set; } = string.Empty;
        public string PhoneNumber { get; set; } = string.Empty;
        public decimal Balance { get; set; }
        public bool IsActive { get; set; }
        public DateTime CreatedDate { get; set; }
    }

    public class OrganizationResultDto
    {
        public long Id { get; set; }
        public required string ResultMessage { get; set; }
    }
}
