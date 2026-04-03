using Domain.Entities;

namespace Domain.Repositories
{
    public interface IClientRepository
    {
        Task<ClientEntity?> GetByIdAsync(long id);
        Task<List<ClientEntity>> GetAllAsync();
        Task<ClientEntity?> GetByPhoneNumberAsync(string phoneNumber);
        Task<ClientEntity> CreateAsync(ClientEntity client);
        Task<ClientEntity> UpdateAsync(ClientEntity client);
        Task DeleteAsync(long id);
    }
}
