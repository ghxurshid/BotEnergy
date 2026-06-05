namespace Domain.Entities
{
    /// <summary>
    /// Platform guruhidagi rol (jadval: auth.platform_roles).
    /// <see cref="MerchantId"/> null bo'lsa — Manage (global) roli;
    /// to'ldirilgan bo'lsa — shu merchantga tegishli rol.
    /// </summary>
    public class PlatformRoleEntity : RoleBase
    {
        /// <summary>null = Manage/global rol; set = merchant scope roli.</summary>
        public long? MerchantId { get; set; }
        public MerchantEntity? Merchant { get; set; }

        public ICollection<PlatformRolePermissionEntity>? RolePermissions { get; set; }
    }
}
