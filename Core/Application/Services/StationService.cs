using Domain.Constants;
using Domain.Dtos;
using Domain.Dtos.Base;
using Domain.Entities;
using Domain.Interfaces;
using Domain.Repositories;

namespace Application.Services
{
    public class StationService : IStationService
    {
        private readonly IStationRepository _repo;
        private readonly IMerchantRepository _merchantRepo;
        private readonly IUserRepository _userRepo;

        public StationService(IStationRepository repo, IMerchantRepository merchantRepo, IUserRepository userRepo)
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
                if (caller is MerchantUserEntity merchantUser)
                {
                    var callerStation = await _repo.GetByIdAsync(merchantUser.StationId);
                    if (callerStation?.MerchantId != dto.MerchantId)
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
                Location = dto.Location,
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

        public async Task<GenericDto<List<StationItemDto>>> GetAllAsync()
        {
            var list = await _repo.GetAllAsync();
            return GenericDto<List<StationItemDto>>.Success(list.Select(ToItem).ToList());
        }

        public async Task<GenericDto<List<StationItemDto>>> GetByMerchantAsync(long merchantId)
        {
            var list = await _repo.GetByMerchantIdAsync(merchantId);
            return GenericDto<List<StationItemDto>>.Success(list.Select(ToItem).ToList());
        }

        public async Task<GenericDto<StationItemDto>> GetByIdAsync(long id)
        {
            var station = await _repo.GetByIdAsync(id);
            if (station is null)
                return GenericDto<StationItemDto>.Error(404, "Stansiya topilmadi.");

            return GenericDto<StationItemDto>.Success(ToItem(station));
        }

        public async Task<GenericDto<StationResultDto>> UpdateAsync(long id, UpdateStationDto dto)
        {
            var station = await _repo.GetByIdAsync(id);
            if (station is null)
                return GenericDto<StationResultDto>.Error(404, "Stansiya topilmadi.");

            if (!string.IsNullOrWhiteSpace(dto.Name)) station.Name = dto.Name;
            if (!string.IsNullOrWhiteSpace(dto.Location)) station.Location = dto.Location;
            if (dto.IsActive.HasValue) station.IsActive = dto.IsActive.Value;

            await _repo.UpdateAsync(station);

            return GenericDto<StationResultDto>.Success(new StationResultDto
            {
                Id = station.Id,
                ResultMessage = "Stansiya ma'lumotlari yangilandi."
            });
        }

        public async Task<GenericDto<StationResultDto>> DeleteAsync(long id)
        {
            var station = await _repo.GetByIdAsync(id);
            if (station is null)
                return GenericDto<StationResultDto>.Error(404, "Stansiya topilmadi.");

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
            Location = s.Location,
            MerchantId = s.MerchantId,
            MerchantName = s.Merchant?.CompanyName ?? string.Empty,
            IsActive = s.IsActive,
            CreatedDate = s.CreatedDate
        };
    }
}
