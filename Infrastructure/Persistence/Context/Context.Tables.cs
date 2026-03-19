using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Persistence.Context
{
    public partial class Context
    {
        public DbSet<UserEntity> Users { get; set; }
        public DbSet<OrganizationEntity> Organizations { get; set; }
        public DbSet<RoleEntity> Roles { get; set; }
        public DbSet<RolePermissionEntity> RolePermissions { get; set; }
        public DbSet<StationEntity> Stations { get; set; }
        public DbSet<ProductEntity> Products { get; set; }
        public DbSet<DeviceEntity> Devices { get; set; }
        public DbSet<UsageSessionEntity> UsageSessions { get; set; }
        public DbSet<ClientEntity> Clients { get; set; }
    }
}
