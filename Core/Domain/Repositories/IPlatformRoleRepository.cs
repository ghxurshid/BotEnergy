using Domain.Entities;

namespace Domain.Repositories
{
    public interface IPlatformRoleRepository
    {
        Task<PlatformRoleEntity?> GetByIdAsync(long id);
        Task<PlatformRoleEntity?> GetByIdWithPermissionsAsync(long id);

        /// <summary>
        /// Scope bo'yicha rollarni qaytaradi.
        /// <paramref name="includeManage"/> = true → MerchantId null bo'lgan Manage rollar ham kiradi.
        /// <paramref name="merchantId"/> = null → barcha merchant rollar; aks holda faqat shu merchant.
        /// </summary>
        Task<List<PlatformRoleEntity>> GetByScopeAsync(bool includeManage, long? merchantId);

        Task<PlatformRoleEntity> CreateAsync(PlatformRoleEntity role);
        Task<PlatformRoleEntity> UpdateAsync(PlatformRoleEntity role);
        Task DeleteAsync(long id);

        Task<List<string>> GetPermissionsByRoleIdAsync(long roleId);
        Task<List<string>> GetUserPermissionsAsync(long? roleId);
    }
}
