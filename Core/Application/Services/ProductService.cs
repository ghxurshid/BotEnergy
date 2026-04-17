using Domain.Constants;
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
        private readonly IProductRepository _productRepo;
        private readonly IDeviceRepository _deviceRepo;
        private readonly IStationRepository _stationRepo;
        private readonly IUserRepository _userRepo;

        public ProductService(
            IProductRepository productRepo,
            IDeviceRepository deviceRepo,
            IStationRepository stationRepo,
            IUserRepository userRepo)
        {
            _productRepo = productRepo;
            _deviceRepo = deviceRepo;
            _stationRepo = stationRepo;
            _userRepo = userRepo;
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

        public async Task<GenericDto<ProductResultDto>> CreateAsync(CreateProductDto dto, long callerId, HashSet<string> callerPermissions)
        {
            var device = await _deviceRepo.GetByIdAsync(dto.DeviceId);
            if (device is null)
                return GenericDto<ProductResultDto>.Error(404, "Qurilma topilmadi.");

            if (!DeviceTypeProductMap.IsAllowed(device.DeviceType, dto.ProductType))
            {
                var allowed = string.Join(", ", DeviceTypeProductMap.GetAllowed(device.DeviceType));
                return GenericDto<ProductResultDto>.Error(400,
                    $"{device.DeviceType} uchun '{dto.ProductType}' tanlash mumkin emas. Ruxsat etilgan: {allowed}.");
            }

            if (!callerPermissions.Contains(Permissions.MerchantAdminRegister))
            {
                var station = await _stationRepo.GetByIdAsync(device.StationId);
                if (station is null)
                    return GenericDto<ProductResultDto>.Error(404, "Qurilma stansiyasi topilmadi.");

                var caller = await _userRepo.GetByIdAsync(callerId);
                if (caller is null)
                    return GenericDto<ProductResultDto>.Error(403, "Foydalanuvchi topilmadi.");

                if (!callerPermissions.Contains(Permissions.StationAdminCreate))
                {
                    if (caller is MerchantUserEntity merchantUser && merchantUser.StationId != device.StationId)
                        return GenericDto<ProductResultDto>.Error(403, "Bu qurilma sizning stansiyangizga tegishli emas.");
                    if (caller is LegalUserEntity legalUser && legalUser.OrganizationId != station.OrganizationId)
                        return GenericDto<ProductResultDto>.Error(403, "Bu qurilma sizning tashkilotingizga tegishli emas.");
                }
                else
                {
                    if (caller is LegalUserEntity legalUser && legalUser.OrganizationId != station.OrganizationId)
                        return GenericDto<ProductResultDto>.Error(403, "Bu stansiya sizning tashkilotingizga tegishli emas.");
                    if (caller is MerchantUserEntity merchantUser)
                    {
                        var callerStation = await _stationRepo.GetByIdAsync(merchantUser.StationId);
                        if (callerStation?.MerchantId != station.MerchantId)
                            return GenericDto<ProductResultDto>.Error(403, "Bu stansiya sizning merchantingizga tegishli emas.");
                    }
                }
            }

            var product = new ProductEntity
            {
                Name = dto.Name,
                Description = dto.Description,
                Type = dto.ProductType,
                Unit = dto.Unit,
                Price = dto.Price,
                DeviceId = dto.DeviceId,
                IsActive = dto.IsActive
            };

            var created = await _productRepo.CreateAsync(product);

            return GenericDto<ProductResultDto>.Success(new ProductResultDto
            {
                Id = created.Id,
                ResultMessage = "Mahsulot muvaffaqiyatli yaratildi."
            });
        }

        public async Task<GenericDto<List<ProductItemDto>>> GetAllAsync()
        {
            var list = await _productRepo.GetAllAsync();
            return GenericDto<List<ProductItemDto>>.Success(list.Select(ToItem).ToList());
        }

        public async Task<GenericDto<List<ProductItemDto>>> GetByDeviceAsync(long deviceId)
        {
            var list = await _productRepo.GetByDeviceIdAsync(deviceId);
            return GenericDto<List<ProductItemDto>>.Success(list.Select(ToItem).ToList());
        }

        public async Task<GenericDto<ProductItemDto>> GetByIdAsync(long id)
        {
            var product = await _productRepo.GetByIdAsync(id);
            if (product is null)
                return GenericDto<ProductItemDto>.Error(404, "Mahsulot topilmadi.");

            return GenericDto<ProductItemDto>.Success(ToItem(product));
        }

        public async Task<GenericDto<ProductResultDto>> UpdateAsync(long id, UpdateProductDto dto)
        {
            var product = await _productRepo.GetByIdAsync(id);
            if (product is null)
                return GenericDto<ProductResultDto>.Error(404, "Mahsulot topilmadi.");

            if (!string.IsNullOrWhiteSpace(dto.Name)) product.Name = dto.Name;
            if (dto.Description is not null) product.Description = dto.Description;
            if (dto.Price.HasValue) product.Price = dto.Price.Value;
            if (dto.IsActive.HasValue) product.IsActive = dto.IsActive.Value;

            await _productRepo.UpdateAsync(product);

            return GenericDto<ProductResultDto>.Success(new ProductResultDto
            {
                Id = product.Id,
                ResultMessage = "Mahsulot ma'lumotlari yangilandi."
            });
        }

        public async Task<GenericDto<ProductResultDto>> DeleteAsync(long id)
        {
            var product = await _productRepo.GetByIdAsync(id);
            if (product is null)
                return GenericDto<ProductResultDto>.Error(404, "Mahsulot topilmadi.");

            await _productRepo.DeleteAsync(id);

            return GenericDto<ProductResultDto>.Success(new ProductResultDto
            {
                Id = id,
                ResultMessage = "Mahsulot o'chirildi."
            });
        }

        private static ProductItemDto ToItem(ProductEntity p) => new()
        {
            Id = p.Id,
            Name = p.Name,
            Description = p.Description,
            Type = p.Type,
            Unit = p.Unit,
            Price = p.Price,
            IsActive = p.IsActive,
            DeviceId = p.DeviceId,
            DeviceSerialNumber = p.Device?.SerialNumber ?? string.Empty,
            CreatedDate = p.CreatedDate
        };
    }
}
