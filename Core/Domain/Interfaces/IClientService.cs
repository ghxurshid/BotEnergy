using Domain.Dtos;
using Domain.Dtos.Base;

namespace Domain.Interfaces
{
    public interface IClientService
    {
        Task<GenericDto<ClientResultDto>> CreateAsync(CreateClientDto dto);
        Task<GenericDto<List<ClientItemDto>>> GetAllAsync();
        Task<GenericDto<ClientItemDto>> GetByIdAsync(long id);
        Task<GenericDto<ClientResultDto>> UpdateAsync(long id, UpdateClientDto dto);
        Task<GenericDto<ClientResultDto>> DeleteAsync(long id);
    }
}
