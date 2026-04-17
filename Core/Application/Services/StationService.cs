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
        private readonly IOrganizationRepository _orgRepo;
        private readonly IUserRepository _userRepo;

        public StationService(IStationRepository repo, IOrganizationRepository orgRepo, IUserRepository userRepo)
        {
            _repo = repo;
            _orgRepo = orgRepo;
            _userRepo = userRepo;
        }

        public async Task<GenericDto<StationResultDto>> CreateAsync(CreateStationDto dto, long callerId, HashSet<string> callerPermissions)
        {
            var org = await _orgRepo.GetByIdAsync(dto.OrganizationId);
            if (org is null)
                return GenericDto<StationResultDto>.Error(404, "Tashkilot topilmadi.");

            if (!callerPermissions.Contains(Permissions.OrganizationAdminCreate))
            {
                var caller = await _userRepo.GetByIdAsync(callerId);
                if (caller is LegalUserEntity legalUser)
                {
                    if (legalUser.OrganizationId != dto.OrganizationId)
                        return GenericDto<StationResultDto>.Error(403, "Faqat o'z tashkilotingizga stansiya qo'sha olasiz.");
                }
                else if (caller is MerchantUserEntity)
                {
                    return GenericDto<StationResultDto>.Error(403, "Boshqa tashkilotlarga stansiya qo'shish uchun tegishli ruxsat kerak.");
                }
            }

            var station = new StationEntity
            {
                Name = dto.Name,
                Location = dto.Location,
                OrganizationId = dto.OrganizationId,
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

        public async Task<GenericDto<List<StationItemDto>>> GetByOrganizationAsync(long organizationId)
        {
            var list = await _repo.GetByOrganizationIdAsync(organizationId);
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
            OrganizationId = s.OrganizationId,
            OrganizationName = s.Organization?.Name ?? string.Empty,
            IsActive = s.IsActive,
            CreatedDate = s.CreatedDate
        };
    }
}
