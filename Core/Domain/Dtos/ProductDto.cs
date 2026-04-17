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
        public bool IsActive { get; set; } = true;
    }

    public class UpdateProductDto
    {
        public string? Name { get; set; }
        public string? Description { get; set; }
        public decimal? Price { get; set; }
        public bool? IsActive { get; set; }
    }

    public class ProductItemDto
    {
        public long Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public ProductType Type { get; set; }
        public UnitType Unit { get; set; }
        public decimal Price { get; set; }
        public bool IsActive { get; set; }
        public long DeviceId { get; set; }
        public string DeviceSerialNumber { get; set; } = string.Empty;
        public DateTime CreatedDate { get; set; }
    }

    public class ProductResultDto
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
