using Domain.Entities;

namespace Domain.Repositories
{
    public interface ICustomerRoleRepository
    {
        Task<CustomerRoleEntity?> GetByIdAsync(long id);
        Task<CustomerRoleEntity?> GetByIdWithPermissionsAsync(long id);

        /// <summary>
        /// Scope bo'yicha rollarni qaytaradi.
        /// <paramref name="includeNatural"/> = true → OrganizationId null bo'lgan global Natural rollar ham kiradi.
        /// <paramref name="organizationId"/> = null → barcha corporate rollar; aks holda faqat shu tashkilot.
        /// </summary>
        Task<List<CustomerRoleEntity>> GetByScopeAsync(bool includeNatural, long? organizationId);

        /// <summary>Default global Natural rolni qaytaradi (self-register'da biriktirish uchun).</summary>
        Task<CustomerRoleEntity?> GetDefaultNaturalRoleAsync();

        Task<CustomerRoleEntity> CreateAsync(CustomerRoleEntity role);
        Task<CustomerRoleEntity> UpdateAsync(CustomerRoleEntity role);
        Task DeleteAsync(long id);

        Task<List<string>> GetPermissionsByRoleIdAsync(long roleId);
        Task<List<string>> GetUserPermissionsAsync(long? roleId);
    }
}
