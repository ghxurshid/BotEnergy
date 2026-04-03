using Domain.Dtos;
using Domain.Dtos.Base;
using Domain.Entities;
using Domain.Enums;
using Domain.Interfaces;
using Domain.Repositories;

namespace Application.Services
{
    public class ProductService : IProductService
    {
        private readonly IProductRepository _productRepository;
        private readonly IDeviceRepository _deviceRepository;

        public ProductService(IProductRepository productRepository, IDeviceRepository deviceRepository)
        {
            _productRepository = productRepository;
            _deviceRepository = deviceRepository;
        }

        public GenericDto<AllowedProductTypesResultDto> GetAllowedProductTypes(DeviceType deviceType)
        {
            var allowed = DeviceTypeProductMap.GetAllowed(deviceType);

            return GenericDto<AllowedProductTypesResultDto>.Success(new AllowedProductTypesResultDto
            {
                DeviceType = deviceType,
                AllowedProductTypes = allowed.Select(p => p.ToString())
            });
        }

        public async Task<GenericDto<CreateProductResultDto>> CreateAsync(CreateProductDto dto)
        {
            var device = await _deviceRepository.GetByIdAsync(dto.DeviceId);
            if (device is null)
                return GenericDto<CreateProductResultDto>.Error(404, "Qurilma topilmadi.");

            if (!DeviceTypeProductMap.IsAllowed(device.DeviceType, dto.ProductType))
            {
                var allowed = string.Join(", ", DeviceTypeProductMap.GetAllowed(device.DeviceType));
                return GenericDto<CreateProductResultDto>.Error(400,
                    $"{device.DeviceType} uchun '{dto.ProductType}' tanlash mumkin emas. Ruxsat etilgan: {allowed}.");
            }

            var product = new ProductEntity
            {
                Name = dto.Name,
                Description = dto.Description,
                Type = dto.ProductType,
                Unit = dto.Unit,
                Price = dto.Price,
                DeviceId = dto.DeviceId,
                IsActive = true
            };

            await _productRepository.CreateAsync(product);

            return GenericDto<CreateProductResultDto>.Success(new CreateProductResultDto
            {
                Id = product.Id,
                ResultMessage = "Mahsulot muvaffaqiyatli yaratildi."
            });
        }
    }
}
