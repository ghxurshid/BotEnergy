using Domain.Auth;
using Domain.Constants;
using Domain.Dtos;
using Domain.Dtos.Base;
using Domain.Entities;
using Domain.Guards;
using Domain.Interfaces;
using Domain.Repositories;
using NetTopologySuite.Geometries;

namespace Application.Services
{
    public class StationService : IStationService
    {
        private readonly IStationRepository _repo;
        private readonly IMerchantRepository _merchantRepo;
        private readonly IPlatformUserRepository _userRepo;
        private readonly IUsageProbeRepository _usageProbe;

        public StationService(
            IStationRepository repo,
            IMerchantRepository merchantRepo,
            IPlatformUserRepository userRepo,
            IUsageProbeRepository usageProbe)
        {
            _repo = repo;
            _merchantRepo = merchantRepo;
            _userRepo = userRepo;
            _usageProbe = usageProbe;
        }

        public async Task<GenericDto<StationResultDto>> CreateAsync(CreateStationDto dto, long callerId, HashSet<string> callerPermissions)
        {
            var merchant = await _merchantRepo.GetByIdAsync(dto.MerchantId);

            var stop = StopFactorCheck.For(StopActions.StationCreate)
                .StopIf(merchant is null, StopFactors.Merchant.NotFound)
                .StopIf(() => !merchant!.IsActive, StopFactors.Merchant.Inactive)
                .Result();

            if (stop is not null)
                return GenericDto<StationResultDto>.Blocked(stop);

            if (!callerPermissions.Contains(Permissions.MerchantAdminRegister))
            {
                var caller = await _userRepo.GetByIdAsync(callerId);
                if (caller is not { Type: Domain.Enums.PlatformUserType.Merchant })
                    return GenericDto<StationResultDto>.Blocked(StopFactors.Access.Denied);

                if (caller.MerchantId != dto.MerchantId)
                    return GenericDto<StationResultDto>.Blocked(StopFactors.Merchant.OutOfScope);
            }

            var station = new StationEntity
            {
                Name = dto.Name,
                Address = dto.Address,
                Coordinates = MakePoint(dto.Latitude, dto.Longitude),
                MerchantId = dto.MerchantId,
                IsActive = true
            };

            var created = await _repo.CreateAsync(station);

            return GenericDto<StationResultDto>.Success(new StationResultDto
            {
                Id = created.Id,
                ResultMessage = "Stansiya muvaffaqiyatli yaratildi."
            });
        }

        public async Task<GenericDto<PagedResult<StationItemDto>>> GetAllAsync(PaginationParams param, AccessScope scope)
        {
            // Platform → hammasi; merchant user → faqat o'z merchanti; aks holda (org/natural) → bo'sh.
            if (!scope.IsManage && scope.MerchantId is null)
                return GenericDto<PagedResult<StationItemDto>>.Success(PagedResult<StationItemDto>.Empty(param));

            var page = await _repo.GetAllAsync(param, scope.IsManage ? null : scope.MerchantId);
            return GenericDto<PagedResult<StationItemDto>>.Success(page.Map(ToItem));
        }

        public async Task<GenericDto<List<StationItemDto>>> GetByMerchantAsync(long merchantId, AccessScope scope)
        {
            if (!scope.CanAccessMerchant(merchantId))
                return GenericDto<List<StationItemDto>>.Blocked(StopFactors.Merchant.OutOfScope);

            var list = await _repo.GetByMerchantIdAsync(merchantId);
            return GenericDto<List<StationItemDto>>.Success(list.Select(ToItem).ToList());
        }

        public async Task<GenericDto<StationItemDto>> GetByIdAsync(long id, AccessScope scope)
        {
            var station = await _repo.GetByIdAsync(id);

            var stop = StopFactorCheck.For("Station.GetById")
                .StopIf(station is null, StopFactors.Station.NotFound)
                .StopIf(() => !scope.CanAccessMerchant(station!.MerchantId), StopFactors.Station.OutOfScope)
                .Result();

            if (stop is not null)
                return GenericDto<StationItemDto>.Blocked(stop);

            return GenericDto<StationItemDto>.Success(ToItem(station!));
        }

        public async Task<GenericDto<StationResultDto>> UpdateAsync(long id, UpdateStationDto dto, AccessScope scope)
        {
            var found = await _repo.GetByIdAsync(id);

            var stop = await StopFactorCheck.For(StopActions.StationUpdate)
                .StopIf(found is null, StopFactors.Station.NotFound)
                .StopIf(() => !scope.CanAccessMerchant(found!.MerchantId), StopFactors.Station.OutOfScope)
                // Stansiyani nofaollashtirish uning qurilmalarini ishdan chiqaradi —
                // ketayotgan sessiyalar oxiriga yetmay qolardi.
                .StopIfAsync(async () => dto.IsActive == false && found!.IsActive
                                         && await _usageProbe.StationHasActiveSessionAsync(id),
                             StopFactors.Station.HasActiveSession)
                .ResultAsync();

            if (stop is not null)
                return GenericDto<StationResultDto>.Blocked(stop);

            var station = found!;

            if (!string.IsNullOrWhiteSpace(dto.Name)) station.Name = dto.Name;
            if (!string.IsNullOrWhiteSpace(dto.Address)) station.Address = dto.Address;
            // Koordinata majburiy — tozalanmaydi; faqat ikkalasi kelsa almashtiriladi (validatsiya filtri juftlikni kafolatlaydi).
            if (dto.Latitude.HasValue && dto.Longitude.HasValue)
                station.Coordinates = MakePoint(dto.Latitude.Value, dto.Longitude.Value);
            if (dto.IsActive.HasValue) station.IsActive = dto.IsActive.Value;

            await _repo.UpdateAsync(station);

            return GenericDto<StationResultDto>.Success(new StationResultDto
            {
                Id = station.Id,
                ResultMessage = "Stansiya ma'lumotlari yangilandi."
            });
        }

        public async Task<GenericDto<StationResultDto>> DeleteAsync(long id, AccessScope scope)
        {
            var station = await _repo.GetByIdAsync(id);

            var stop = await StopFactorCheck.For(StopActions.StationDelete)
                .StopIf(station is null, StopFactors.Station.NotFound)
                .StopIf(() => !scope.CanAccessMerchant(station!.MerchantId), StopFactors.Station.OutOfScope)
                // Soft-delete kaskad qilmaydi: stansiya o'chsa, qurilmalari "egasiz" bo'lib
                // ro'yxatlarda ko'rinishda davom etardi.
                .StopIfCountAsync(() => _usageProbe.StationDeviceCountAsync(id),
                                  StopFactors.Station.HasDevices)
                .ResultAsync();

            if (stop is not null)
                return GenericDto<StationResultDto>.Blocked(stop);

            await _repo.DeleteAsync(id);

            return GenericDto<StationResultDto>.Success(new StationResultDto
            {
                Id = id,
                ResultMessage = "Stansiya o'chirildi."
            });
        }

        private static StationItemDto ToItem(StationEntity s) => new()
        {
            Id = s.Id,
            Name = s.Name,
            Address = s.Address,
            Latitude = (decimal)s.Coordinates.Y,   // Y = kenglik (latitude)
            Longitude = (decimal)s.Coordinates.X,  // X = uzunlik (longitude)
            MerchantId = s.MerchantId,
            MerchantName = s.Merchant?.CompanyName ?? string.Empty,
            IsActive = s.IsActive,
            CreatedDate = s.CreatedDate
        };

        /// <summary>SRID 4326 (WGS84) Point yasaydi. Diqqat: Point(X=uzunlik, Y=kenglik).</summary>
        private static Point MakePoint(decimal latitude, decimal longitude)
            => new Point((double)longitude, (double)latitude) { SRID = 4326 };
    }
}
