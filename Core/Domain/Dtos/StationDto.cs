namespace Domain.Dtos
{
    public class CreateStationDto
    {
        public required string Name { get; set; }
        public string? Location { get; set; }
        public long OrganizationId { get; set; }
    }

    public class UpdateStationDto
    {
        public string? Name { get; set; }
        public string? Location { get; set; }
        public bool? IsActive { get; set; }
    }

    public class StationItemDto
    {
        public long Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Location { get; set; }
        public long OrganizationId { get; set; }
        public string OrganizationName { get; set; } = string.Empty;
        public bool IsActive { get; set; }
        public DateTime CreatedDate { get; set; }
    }

    public class StationResultDto
    {
        public long Id { get; set; }
        public required string ResultMessage { get; set; }
    }
}
