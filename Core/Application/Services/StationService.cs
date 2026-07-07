using Domain.Auth;
using Domain.Constants;
using Domain.Dtos;
using Domain.Dtos.Base;
using Domain.Entities;
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

        public StationService(IStationRepository repo, IMerchantRepository merchantRepo, IPlatformUserRepository userRepo)
        {
            _repo = repo;
            _merchantRepo = merchantRepo;
            _userRepo = userRepo;
        }

        public async Task<GenericDto<StationResultDto>> CreateAsync(CreateStationDto dto, long callerId, HashSet<string> callerPermissions)
        {
            var merchant = await _merchantRepo.GetByIdAsync(dto.MerchantId);
            if (merchant is null)
                return GenericDto<StationResultDto>.Error(404, "Merchant topilmadi.");

            if (!merchant.IsActive)
                return GenericDto<StationResultDto>.Error(400, "Merchant faol emas.");

            if (!callerPermissions.Contains(Permissions.MerchantAdminRegister))
            {
                var caller = await _userRepo.GetByIdAsync(callerId);
                if (caller is { Type: Domain.Enums.PlatformUserType.Merchant })
                {
                    if (caller.MerchantId != dto.MerchantId)
                        return GenericDto<StationResultDto>.Error(403, "Faqat o'z merchantingizga stansiya qo'sha olasiz.");
                }
                else
                {
                    return GenericDto<StationResultDto>.Error(403, "Boshqa merchantlarga stansiya qo'shish uchun tegishli ruxsat kerak.");
                }
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
                return GenericDto<List<StationItemDto>>.Error(403, "Bu merchant sizning doirangizga tegishli emas.");

            var list = await _repo.GetByMerchantIdAsync(merchantId);
            return GenericDto<List<StationItemDto>>.Success(list.Select(ToItem).ToList());
        }

        public async Task<GenericDto<StationItemDto>> GetByIdAsync(long id, AccessScope scope)
        {
            var station = await _repo.GetByIdAsync(id);
            if (station is null)
                return GenericDto<StationItemDto>.Error(404, "Stansiya topilmadi.");

            if (!scope.CanAccessMerchant(station.MerchantId))
                return GenericDto<StationItemDto>.Error(403, "Bu stansiya sizning doirangizga tegishli emas.");

            return GenericDto<StationItemDto>.Success(ToItem(station));
        }

        public async Task<GenericDto<StationResultDto>> UpdateAsync(long id, UpdateStationDto dto, AccessScope scope)
        {
            var station = await _repo.GetByIdAsync(id);
            if (station is null)
                return GenericDto<StationResultDto>.Error(404, "Stansiya topilmadi.");

            if (!scope.CanAccessMerchant(station.MerchantId))
                return GenericDto<StationResultDto>.Error(403, "Bu stansiya sizning doirangizga tegishli emas.");

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
            if (station is null)
                return GenericDto<StationResultDto>.Error(404, "Stansiya topilmadi.");

            if (!scope.CanAccessMerchant(station.MerchantId))
                return GenericDto<StationResultDto>.Error(403, "Bu stansiya sizning doirangizga tegishli emas.");

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
