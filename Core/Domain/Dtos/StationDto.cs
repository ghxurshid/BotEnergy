namespace Domain.Dtos
{
    public class CreateStationDto
    {
        public required string Name { get; set; }
        public required string Address { get; set; }
        public decimal Latitude { get; set; }
        public decimal Longitude { get; set; }
        public long MerchantId { get; set; }
    }

    public class UpdateStationDto
    {
        public string? Name { get; set; }
        public string? Address { get; set; }
        public decimal? Latitude { get; set; }
        public decimal? Longitude { get; set; }
        public bool? IsActive { get; set; }
    }

    public class StationItemDto
    {
        public long Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Address { get; set; } = string.Empty;
        public decimal Latitude { get; set; }
        public decimal Longitude { get; set; }
        public long MerchantId { get; set; }
        public string MerchantName { get; set; } = string.Empty;
        public bool IsActive { get; set; }
        public DateTime CreatedDate { get; set; }
    }

    public class StationResultDto
    {
        public long Id { get; set; }
        public required string ResultMessage { get; set; }
    }
}
