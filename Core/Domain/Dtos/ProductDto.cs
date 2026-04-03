using Domain.Enums;

namespace Domain.Dtos
{
    public class CreateProductDto
    {
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public ProductType ProductType { get; set; }
        public UnitType Unit { get; set; }
        public decimal Price { get; set; }
        public long DeviceId { get; set; }
    }

    public class CreateProductResultDto
    {
        public long Id { get; set; }
        public required string ResultMessage { get; set; }
    }

    public class AllowedProductTypesResultDto
    {
        public DeviceType DeviceType { get; set; }
        public IEnumerable<string> AllowedProductTypes { get; set; } = [];
    }
}
