using Domain.Enums;

namespace AdminApi.Models.Requests
{
    public class CreateProductRequest
    {
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public ProductType ProductType { get; set; }
        public UnitType Unit { get; set; }
        public decimal Price { get; set; }
        public long DeviceId { get; set; }
        public bool? IsActive { get; set; }
    }
}
