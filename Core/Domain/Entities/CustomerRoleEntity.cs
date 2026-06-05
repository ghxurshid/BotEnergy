namespace Domain.Entities
{
    /// <summary>
    /// Customer guruhidagi rol (jadval: auth.customer_roles).
    /// <see cref="OrganizationId"/> null bo'lsa — global Natural rol (default);
    /// to'ldirilgan bo'lsa — shu tashkilotga tegishli Corporate rol.
    /// </summary>
    public class CustomerRoleEntity : RoleBase
    {
        /// <summary>null = global Natural rol; set = corporate org roli.</summary>
        public long? OrganizationId { get; set; }
        public OrganizationEntity? Organization { get; set; }

        public ICollection<CustomerRolePermissionEntity>? RolePermissions { get; set; }
    }
}
