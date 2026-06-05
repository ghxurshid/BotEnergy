using Domain.Entities;

namespace Domain.Repositories
{
    /// <summary>Umumiy permission katalogi (auth.permissions) — har ikkala rol guruhi ham foydalanadi.</summary>
    public interface IPermissionRepository
    {
        Task<PermissionEntity?> GetByNameAsync(string name);
        Task<PermissionEntity?> GetByIdAsync(long id);
        Task<List<PermissionEntity>> GetAllAsync();
        Task<List<long>> FilterExistingIdsAsync(IEnumerable<long> ids);
    }
}
