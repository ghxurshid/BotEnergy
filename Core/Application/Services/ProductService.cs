using Domain.Auth;
using Domain.Constants;
using Domain.Dtos;
using Domain.Dtos.Base;
using Domain.Entities;
using Domain.Enums;
using Domain.Guards;
using Domain.Interfaces;
using Domain.Repositories;

namespace Application.Services
{
    public class ProductService : IProductService
    {
        private readonly IProductRepository _productRepo;
        private readonly IDeviceRepository _deviceRepo;
        private readonly IStationRepository _stationRepo;
        private readonly IPlatformUserRepository _userRepo;
        private readonly IUsageProbeRepository _usageProbe;

        public ProductService(
            IProductRepository productRepo,
            IDeviceRepository deviceRepo,
            IStationRepository stationRepo,
            IPlatformUserRepository userRepo,
            IUsageProbeRepository usageProbe)
        {
            _productRepo = productRepo;
            _deviceRepo = deviceRepo;
            _stationRepo = stationRepo;
            _userRepo = userRepo;
            _usageProbe = usageProbe;
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

            var stop = StopFactorCheck.For(StopActions.ProductCreate)
                .StopIf(device is null, StopFactors.Device.NotFound)
                .StopIf(() => !device!.IsActive, StopFactors.Device.Inactive)
                .StopIf(() => !DeviceTypeProductMap.IsAllowed(device!.DeviceType, dto.ProductType),
                        () => StopFactors.Product.TypeNotAllowed(
                            device!.DeviceType, dto.ProductType,
                            string.Join(", ", DeviceTypeProductMap.GetAllowed(device.DeviceType))))
                .Result();

            if (stop is not null)
                return GenericDto<ProductResultDto>.Blocked(stop);

            if (!callerPermissions.Contains(Permissions.MerchantAdminRegister))
            {
                var station = await _stationRepo.GetByIdAsync(device!.StationId);
                var caller = await _userRepo.GetByIdAsync(callerId);

                var accessStop = StopFactorCheck.For(StopActions.ProductCreate)
                    .StopIf(station is null, StopFactors.Station.NotFound)
                    .StopIf(() => !station!.IsActive, StopFactors.Station.Inactive)
                    .StopIf(caller is null, StopFactors.User.NotFound)
                    .StopIf(() => caller!.Type == PlatformUserType.Merchant
                                  && caller.MerchantId != station!.MerchantId,
                            StopFactors.Station.OutOfScope)
                    .Result();

                if (accessStop is not null)
                    return GenericDto<ProductResultDto>.Blocked(accessStop);
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

        public async Task<GenericDto<PagedResult<ProductItemDto>>> GetAllAsync(PaginationParams param, AccessScope scope)
        {
            if (!scope.IsManage && scope.MerchantId is null)
                return GenericDto<PagedResult<ProductItemDto>>.Success(PagedResult<ProductItemDto>.Empty(param));

            var page = await _productRepo.GetAllAsync(param, scope.IsManage ? null : scope.MerchantId);
            return GenericDto<PagedResult<ProductItemDto>>.Success(page.Map(ToItem));
        }

        public async Task<GenericDto<List<ProductItemDto>>> GetByDeviceAsync(long deviceId, AccessScope scope)
        {
            var device = await _deviceRepo.GetByIdAsync(deviceId);

            var stop = StopFactorCheck.For("Product.GetByDevice")
                .StopIf(device is null, StopFactors.Device.NotFound)
                .StopIf(() => !scope.IsManage
                              && (device!.Station is null || device.Station.MerchantId != scope.MerchantId),
                        StopFactors.Device.OutOfScope)
                .Result();

            if (stop is not null)
                return GenericDto<List<ProductItemDto>>.Blocked(stop);

            var list = await _productRepo.GetByDeviceIdAsync(deviceId);
            return GenericDto<List<ProductItemDto>>.Success(list.Select(ToItem).ToList());
        }

        public async Task<GenericDto<ProductItemDto>> GetByIdAsync(long id, AccessScope scope)
        {
            var product = await _productRepo.GetByIdAsync(id);

            var stop = StopFactorCheck.For("Product.GetById")
                .StopIf(product is null, StopFactors.Product.NotFound)
                .StopIf(() => OutOfScope(product!, scope), StopFactors.Product.OutOfScope)
                .Result();

            if (stop is not null)
                return GenericDto<ProductItemDto>.Blocked(stop);

            return GenericDto<ProductItemDto>.Success(ToItem(product!));
        }

        public async Task<GenericDto<ProductResultDto>> UpdateAsync(long id, UpdateProductDto dto, AccessScope scope)
        {
            var found = await _productRepo.GetByIdAsync(id);

            var stop = await StopFactorCheck.For(StopActions.ProductUpdate)
                .StopIf(found is null, StopFactors.Product.NotFound)
                .StopIf(() => OutOfScope(found!, scope), StopFactors.Product.OutOfScope)
                // Narx yoki faollik ketayotgan jarayon ostida o'zgarsa, mijoz boshqa narxda
                // boshlagan xizmat uchun boshqa summa to'lardi.
                .StopIfAsync(async () => (dto.Price.HasValue || dto.IsActive == false)
                                         && await _usageProbe.ProductHasActiveProcessAsync(id),
                             StopFactors.Product.InUse)
                .ResultAsync();

            if (stop is not null)
                return GenericDto<ProductResultDto>.Blocked(stop);

            var product = found!;

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

        public async Task<GenericDto<ProductResultDto>> DeleteAsync(long id, AccessScope scope)
        {
            var product = await _productRepo.GetByIdAsync(id);

            var stop = await StopFactorCheck.For(StopActions.ProductDelete)
                .StopIf(product is null, StopFactors.Product.NotFound)
                .StopIf(() => OutOfScope(product!, scope), StopFactors.Product.OutOfScope)
                .StopIfAsync(() => _usageProbe.ProductHasActiveProcessAsync(id), StopFactors.Product.InUse)
                .ResultAsync();

            if (stop is not null)
                return GenericDto<ProductResultDto>.Blocked(stop);

            await _productRepo.DeleteAsync(id);

            return GenericDto<ProductResultDto>.Success(new ProductResultDto
            {
                Id = id,
                ResultMessage = "Mahsulot o'chirildi."
            });
        }

        /// <summary>
        /// Mahsulot caller scope'idan tashqaridami
        /// (product → device → station.MerchantId). Manage har doim o'tadi.
        /// </summary>
        private static bool OutOfScope(ProductEntity product, AccessScope scope)
        {
            if (scope.IsManage)
                return false;

            var merchantId = product.Device?.Station?.MerchantId;
            return merchantId is null || merchantId != scope.MerchantId;
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
