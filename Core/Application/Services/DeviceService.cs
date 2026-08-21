using Domain.Auth;
using Domain.Constants;
using Domain.Dtos;
using Domain.Dtos.Base;
using Domain.Entities;
using Domain.Guards;
using Domain.Interfaces;
using Domain.Repositories;

namespace Application.Services
{
    public class DeviceService : IDeviceService
    {
        private readonly IDeviceRepository _repo;
        private readonly IStationRepository _stationRepo;
        private readonly IPlatformUserRepository _userRepo;
        private readonly IUsageProbeRepository _usageProbe;

        public DeviceService(
            IDeviceRepository repo,
            IStationRepository stationRepo,
            IPlatformUserRepository userRepo,
            IUsageProbeRepository usageProbe)
        {
            _repo = repo;
            _stationRepo = stationRepo;
            _userRepo = userRepo;
            _usageProbe = usageProbe;
        }

        public async Task<GenericDto<DeviceResultDto>> RegisterAsync(RegisterDeviceDto dto, long callerId, HashSet<string> callerPermissions)
        {
            var station = await _stationRepo.GetByIdAsync(dto.StationId);

            var stop = StopFactorCheck.For(StopActions.DeviceRegister)
                .StopIf(station is null, StopFactors.Station.NotFound)
                .StopIf(() => !station!.IsActive, StopFactors.Station.Inactive)
                .Result();

            if (stop is not null)
                return GenericDto<DeviceResultDto>.Blocked(stop);

            // Doira tekshiruvi seriya raqamidan OLDIN: aks holda begona merchantning
            // qurilma seriyasi band-yo'qligini tashqi odam bilib olardi.
            var accessCheck = await CheckStationAccessAsync(callerId, callerPermissions, station!);
            if (accessCheck is not null)
                return accessCheck;

            // Nofaol qurilma ham unique indexni band qiladi — shu sabab IsActive'siz tekshiriladi.
            if (await _repo.ExistsBySerialNumberAsync(dto.SerialNumber))
                return GenericDto<DeviceResultDto>.Blocked(StopFactors.Device.SerialTaken(dto.SerialNumber));

            var device = new DeviceEntity
            {
                SerialNumber = dto.SerialNumber,
                DeviceType = dto.DeviceType,
                StationId = dto.StationId,
                Model = dto.Model,
                FirmwareVersion = dto.FirmwareVersion,
                IsOnline = dto.IsOnline,
                IsActive = dto.IsActive
            };

            var created = await _repo.CreateAsync(device);

            return GenericDto<DeviceResultDto>.Success(new DeviceResultDto
            {
                Id = created.Id,
                ResultMessage = "Qurilma muvaffaqiyatli ro'yxatdan o'tkazildi."
            });
        }

        public async Task<GenericDto<PagedResult<DeviceItemDto>>> GetAllAsync(PaginationParams param, AccessScope scope)
        {
            if (!scope.IsManage && scope.MerchantId is null)
                return GenericDto<PagedResult<DeviceItemDto>>.Success(PagedResult<DeviceItemDto>.Empty(param));

            var page = await _repo.GetAllAsync(param, scope.IsManage ? null : scope.MerchantId);
            return GenericDto<PagedResult<DeviceItemDto>>.Success(page.Map(ToItem));
        }

        public async Task<GenericDto<List<DeviceItemDto>>> GetByStationAsync(long stationId, AccessScope scope)
        {
            var station = await _stationRepo.GetByIdAsync(stationId);

            var stop = StopFactorCheck.For("Device.GetByStation")
                .StopIf(station is null, StopFactors.Station.NotFound)
                .StopIf(() => !scope.CanAccessMerchant(station!.MerchantId), StopFactors.Station.OutOfScope)
                .Result();

            if (stop is not null)
                return GenericDto<List<DeviceItemDto>>.Blocked(stop);

            var list = await _repo.GetByStationIdAsync(stationId);
            return GenericDto<List<DeviceItemDto>>.Success(list.Select(ToItem).ToList());
        }

        public async Task<GenericDto<DeviceItemDto>> GetByIdAsync(long id, AccessScope scope)
        {
            var device = await _repo.GetByIdAsync(id);

            var stop = StopFactorCheck.For("Device.GetById")
                .StopIf(device is null, StopFactors.Device.NotFound)
                .StopIf(() => OutOfScope(device!, scope), StopFactors.Device.OutOfScope)
                .Result();

            if (stop is not null)
                return GenericDto<DeviceItemDto>.Blocked(stop);

            return GenericDto<DeviceItemDto>.Success(ToItem(device!));
        }

        public async Task<GenericDto<DeviceResultDto>> UpdateAsync(long id, UpdateDeviceDto dto, AccessScope scope)
        {
            var found = await _repo.GetByIdAsync(id);

            var stop = await StopFactorCheck.For(StopActions.DeviceUpdate)
                .StopIf(found is null, StopFactors.Device.NotFound)
                .StopIf(() => OutOfScope(found!, scope), StopFactors.Device.OutOfScope)
                // Nofaollashtirish — bu qurilmani ishdan chiqarish: ustida ketayotgan sessiya
                // buyruqsiz osilib qolardi (mijoz pulini to'lab, xizmatni ololmay).
                .StopIfAsync(async () => dto.IsActive == false && found!.IsActive
                                         && await _usageProbe.DeviceHasActiveSessionAsync(id),
                             StopFactors.Device.HasActiveSession)
                .StopIfAsync(async () => dto.IsActive == false && found!.IsActive
                                         && await _usageProbe.DeviceHasOpenCashSessionAsync(id),
                             StopFactors.Device.HasOpenCashSession)
                .ResultAsync();

            if (stop is not null)
                return GenericDto<DeviceResultDto>.Blocked(stop);

            var device = found!;

            if (!string.IsNullOrWhiteSpace(dto.Model)) device.Model = dto.Model;
            if (!string.IsNullOrWhiteSpace(dto.FirmwareVersion)) device.FirmwareVersion = dto.FirmwareVersion;
            if (dto.IsOnline.HasValue) device.IsOnline = dto.IsOnline.Value;
            if (dto.IsActive.HasValue) device.IsActive = dto.IsActive.Value;

            await _repo.UpdateAsync(device);

            return GenericDto<DeviceResultDto>.Success(new DeviceResultDto
            {
                Id = device.Id,
                ResultMessage = "Qurilma ma'lumotlari yangilandi."
            });
        }

        public async Task<GenericDto<DeviceResultDto>> DeleteAsync(long id, AccessScope scope)
        {
            var device = await _repo.GetByIdAsync(id);

            var stop = await StopFactorCheck.For(StopActions.DeviceDelete)
                .StopIf(device is null, StopFactors.Device.NotFound)
                .StopIf(() => OutOfScope(device!, scope), StopFactors.Device.OutOfScope)
                .StopIfAsync(() => _usageProbe.DeviceHasActiveSessionAsync(id),
                             StopFactors.Device.HasActiveSession)
                .StopIfAsync(() => _usageProbe.DeviceHasOpenCashSessionAsync(id),
                             StopFactors.Device.HasOpenCashSession)
                .StopIfAsync(() => _usageProbe.DeviceHasOpenCollectionAsync(id),
                             StopFactors.Device.HasOpenCollection)
                // Qoldiq nolga tushmasdan o'chirilsa, boxdagi pul hisobdan yo'qoladi.
                .StopIf(() => device!.CashBalance > 0,
                        () => StopFactors.Device.HasCash(device!.CashBalance))
                .ResultAsync();

            if (stop is not null)
                return GenericDto<DeviceResultDto>.Blocked(stop);

            await _repo.DeleteAsync(id);

            return GenericDto<DeviceResultDto>.Success(new DeviceResultDto
            {
                Id = id,
                ResultMessage = "Qurilma o'chirildi."
            });
        }

        private async Task<GenericDto<DeviceResultDto>?> CheckStationAccessAsync(
            long callerId, HashSet<string> callerPermissions, StationEntity station)
        {
            if (callerPermissions.Contains(Permissions.MerchantAdminRegister))
                return null;

            var caller = await _userRepo.GetByIdAsync(callerId);
            if (caller is null)
                return GenericDto<DeviceResultDto>.Blocked(StopFactors.User.NotFound);

            if (caller.Type != Domain.Enums.PlatformUserType.Merchant)
                return GenericDto<DeviceResultDto>.Blocked(StopFactors.Access.Denied);

            if (caller.MerchantId != station.MerchantId)
                return GenericDto<DeviceResultDto>.Blocked(StopFactors.Station.OutOfScope);

            return null;
        }

        /// <summary>
        /// Qurilma caller scope'idan tashqaridami (device → station.MerchantId).
        /// Manage har doim o'tadi; merchant operator faqat o'z merchanti.
        /// </summary>
        private static bool OutOfScope(DeviceEntity device, AccessScope scope)
        {
            if (scope.IsManage)
                return false;

            var merchantId = device.Station?.MerchantId;
            return merchantId is null || merchantId != scope.MerchantId;
        }

        private static DeviceItemDto ToItem(DeviceEntity d) => new()
        {
            Id = d.Id,
            SerialNumber = d.SerialNumber,
            DeviceType = d.DeviceType,
            Model = d.Model,
            FirmwareVersion = d.FirmwareVersion,
            StationId = d.StationId,
            StationName = d.Station?.Name ?? string.Empty,
            IsOnline = d.IsOnline,
            IsActive = d.IsActive,
            CreatedDate = d.CreatedDate,
            CashBalance = d.CashBalance,
            CashLastCollectedAt = d.CashLastCollectedAt
        };
    }
}
