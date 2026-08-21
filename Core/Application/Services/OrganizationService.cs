using Domain.Auth;
using Domain.Dtos;
using Domain.Dtos.Base;
using Domain.Entities;
using Domain.Guards;
using Domain.Interfaces;
using Domain.Repositories;

namespace Application.Services
{
    public class OrganizationService : IOrganizationService
    {
        private readonly IOrganizationRepository _repo;
        private readonly IUsageProbeRepository _usageProbe;

        public OrganizationService(IOrganizationRepository repo, IUsageProbeRepository usageProbe)
        {
            _repo = repo;
            _usageProbe = usageProbe;
        }

        public async Task<GenericDto<OrganizationResultDto>> CreateAsync(CreateOrganizationDto dto)
        {
            var org = new OrganizationEntity
            {
                Name = dto.Name,
                Inn = dto.Inn,
                Address = dto.Address,
                PhoneNumber = dto.PhoneNumber,
                Balance = dto.Balance,
                IsActive = dto.IsActive
            };

            var created = await _repo.CreateAsync(org);

            return GenericDto<OrganizationResultDto>.Success(new OrganizationResultDto
            {
                Id = created.Id,
                ResultMessage = "Tashkilot muvaffaqiyatli yaratildi."
            });
        }

        public async Task<GenericDto<PagedResult<OrganizationItemDto>>> GetAllAsync(PaginationParams param, AccessScope scope)
        {
            // Platform → hammasi; org user → faqat o'z tashkiloti; aks holda bo'sh.
            if (!scope.IsManage && scope.OrganizationId is null)
                return GenericDto<PagedResult<OrganizationItemDto>>.Success(PagedResult<OrganizationItemDto>.Empty(param));

            var page = await _repo.GetAllAsync(param, scope.IsManage ? null : scope.OrganizationId);
            return GenericDto<PagedResult<OrganizationItemDto>>.Success(page.Map(ToItem));
        }

        public async Task<GenericDto<OrganizationItemDto>> GetByIdAsync(long id, AccessScope scope)
        {
            if (!scope.CanAccessOrganization(id))
                return GenericDto<OrganizationItemDto>.Blocked(StopFactors.Organization.OutOfScope);

            var org = await _repo.GetByIdAsync(id);
            if (org is null)
                return GenericDto<OrganizationItemDto>.Blocked(StopFactors.Organization.NotFound);

            return GenericDto<OrganizationItemDto>.Success(ToItem(org));
        }

        public async Task<GenericDto<OrganizationResultDto>> UpdateAsync(long id, UpdateOrganizationDto dto, AccessScope scope)
        {
            if (!scope.CanAccessOrganization(id))
                return GenericDto<OrganizationResultDto>.Blocked(StopFactors.Organization.OutOfScope);

            var org = await _repo.GetByIdAsync(id);
            if (org is null)
                return GenericDto<OrganizationResultDto>.Blocked(StopFactors.Organization.NotFound);

            if (!string.IsNullOrWhiteSpace(dto.Address)) org.Address = dto.Address;
            if (!string.IsNullOrWhiteSpace(dto.PhoneNumber)) org.PhoneNumber = dto.PhoneNumber;
            if (dto.IsActive.HasValue) org.IsActive = dto.IsActive.Value;

            await _repo.UpdateAsync(org);

            return GenericDto<OrganizationResultDto>.Success(new OrganizationResultDto
            {
                Id = org.Id,
                ResultMessage = "Tashkilot ma'lumotlari yangilandi."
            });
        }

        public async Task<GenericDto<OrganizationResultDto>> DeleteAsync(long id, AccessScope scope)
        {
            var org = await _repo.GetByIdAsync(id);

            var stop = await StopFactorCheck.For(StopActions.OrganizationDelete)
                .StopIf(!scope.CanAccessOrganization(id), StopFactors.Organization.OutOfScope)
                .StopIf(org is null, StopFactors.Organization.NotFound)
                // Xodimlar tashkilot balansidan to'laydi — tashkilot o'chsa ular
                // balanssiz qolib, sessiya ocholmay qolardi.
                .StopIfCountAsync(() => _usageProbe.OrganizationUserCountAsync(id),
                                  StopFactors.Organization.HasUsers)
                // Qoldiq pul hisobdan yo'qolmasligi kerak.
                .StopIf(() => org!.Balance > 0, () => StopFactors.Organization.HasBalance(org!.Balance))
                .ResultAsync();

            if (stop is not null)
                return GenericDto<OrganizationResultDto>.Blocked(stop);

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
