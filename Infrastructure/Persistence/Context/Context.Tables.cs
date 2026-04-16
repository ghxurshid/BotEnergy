using Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Persistence.Context
{
    public partial class AppDbContext
    {
        public DbSet<UserEntity> Users { get; set; }
        public DbSet<OrganizationEntity> Organizations { get; set; }
        public DbSet<RoleEntity> Roles { get; set; }
        public DbSet<PermissionEntity> Permissions { get; set; }
        public DbSet<RolePermissionEntity> RolePermissions { get; set; }
        public DbSet<UserRoleEntity> UserRoles { get; set; }
        public DbSet<StationEntity> Stations { get; set; }
        public DbSet<ProductEntity> Products { get; set; }
        public DbSet<DeviceEntity> Devices { get; set; }
        public DbSet<UsageSessionEntity> UsageSessions { get; set; }
        public DbSet<MerchantEntity> Merchants { get; set; }
    }
}
