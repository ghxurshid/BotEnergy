using Domain.Dtos;
using Domain.Dtos.Base;
using Domain.Entities;
using Domain.Interfaces;
using Domain.Repositories;

namespace Application.Services
{
    public class OrganizationService : IOrganizationService
    {
        private readonly IOrganizationRepository _repo;

        public OrganizationService(IOrganizationRepository repo)
            => _repo = repo;

        public async Task<GenericDto<OrganizationResultDto>> CreateAsync(CreateOrganizationDto dto)
        {
            var org = new OrganizationEntity
            {
                Name = dto.Name,
                Inn = dto.Inn,
                Address = dto.Address,
                PhoneNumber = dto.PhoneNumber,
                IsActive = true
            };

            var created = await _repo.CreateAsync(org);

            return GenericDto<OrganizationResultDto>.Success(new OrganizationResultDto
            {
                Id = created.Id,
                ResultMessage = "Tashkilot muvaffaqiyatli yaratildi."
            });
        }

        public async Task<GenericDto<List<OrganizationItemDto>>> GetAllAsync()
        {
            var list = await _repo.GetAllAsync();
            return GenericDto<List<OrganizationItemDto>>.Success(list.Select(ToItem).ToList());
        }

        public async Task<GenericDto<OrganizationItemDto>> GetByIdAsync(long id)
        {
            var org = await _repo.GetByIdAsync(id);
            if (org is null)
                return GenericDto<OrganizationItemDto>.Error(404, "Tashkilot topilmadi.");

            return GenericDto<OrganizationItemDto>.Success(ToItem(org));
        }

        public async Task<GenericDto<OrganizationResultDto>> UpdateAsync(long id, UpdateOrganizationDto dto)
        {
            var org = await _repo.GetByIdAsync(id);
            if (org is null)
                return GenericDto<OrganizationResultDto>.Error(404, "Tashkilot topilmadi.");

            if (!string.IsNullOrWhiteSpace(dto.Name)) org.Name = dto.Name;
            if (!string.IsNullOrWhiteSpace(dto.Inn)) org.Inn = dto.Inn;
            if (!string.IsNullOrWhiteSpace(dto.Address)) org.Address = dto.Address;
            if (!string.IsNullOrWhiteSpace(dto.PhoneNumber)) org.PhoneNumber = dto.PhoneNumber;

            await _repo.UpdateAsync(org);

            return GenericDto<OrganizationResultDto>.Success(new OrganizationResultDto
            {
                Id = org.Id,
                ResultMessage = "Tashkilot ma'lumotlari yangilandi."
            });
        }

        public async Task<GenericDto<OrganizationResultDto>> DeleteAsync(long id)
        {
            var org = await _repo.GetByIdAsync(id);
            if (org is null)
                return GenericDto<OrganizationResultDto>.Error(404, "Tashkilot topilmadi.");

            await _repo.DeleteAsync(id);

            return GenericDto<OrganizationResultDto>.Success(new OrganizationResultDto
            {
                Id = id,
                ResultMessage = "Tashkilot o'chirildi."
            });
        }

        private static OrganizationItemDto ToItem(OrganizationEntity o) => new()
        {
            Id = o.Id,
            Name = o.Name,
            Inn = o.Inn,
            Address = o.Address,
            PhoneNumber = o.PhoneNumber,
            Balance = o.Balance,
            IsActive = o.IsActive,
            CreatedDate = o.CreatedDate
        };
    }
}
