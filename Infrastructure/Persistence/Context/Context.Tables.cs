using Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Persistence.Context
{
    public partial class AppDbContext
    {
        public DbSet<PlatformUserEntity> PlatformUsers { get; set; }
        public DbSet<CustomerUserEntity> CustomerUsers { get; set; }
        public DbSet<OrganizationEntity> Organizations { get; set; }
        public DbSet<PlatformRoleEntity> PlatformRoles { get; set; }
        public DbSet<CustomerRoleEntity> CustomerRoles { get; set; }
        public DbSet<PermissionEntity> Permissions { get; set; }
        public DbSet<PlatformRolePermissionEntity> PlatformRolePermissions { get; set; }
        public DbSet<CustomerRolePermissionEntity> CustomerRolePermissions { get; set; }
        public DbSet<StationEntity> Stations { get; set; }
        public DbSet<ProductEntity> Products { get; set; }
        public DbSet<DeviceEntity> Devices { get; set; }
        public DbSet<SessionEntity> Sessions { get; set; }
        public DbSet<ProductProcessEntity> ProductProcesses { get; set; }
        public DbSet<MerchantEntity> Merchants { get; set; }
        public DbSet<PaymentTransactionEntity> PaymentTransactions { get; set; }
        public DbSet<PaymentTransactionStepEntity> PaymentTransactionSteps { get; set; }
    }
}
