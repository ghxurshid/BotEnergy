using Application.Helpers;
using Domain.Constants;
using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Persistence.Context;

namespace Persistence.Seed
{
    public static class DataSeeder
    {
        private const string DefaultOrganizationName = "BotEnergy System";
        private const string DefaultRoleName = "SuperAdmin";
        private const string DefaultPhoneNumber = "998901234567";
        private const string DefaultPassword = "Admin@123";
        private const string DefaultMail = "admin@botenergy.uz";
        private const string DefaultPhoneId = "default-admin-device";

        public static async Task SeedAsync(AppDbContext context)
        {
            var organization = await SeedDefaultOrganizationAsync(context);
            var role = await SeedDefaultRoleAsync(context, organization.Id);
            await SeedRolePermissionsAsync(context, role);
            await SeedDefaultUserAsync(context, role.Id);
        }

        private static async Task<OrganizationEntity> SeedDefaultOrganizationAsync(AppDbContext context)
        {
            var organization = await context.Organizations
                .FirstOrDefaultAsync(o => o.Name == DefaultOrganizationName && !o.IsDeleted);

            if (organization is not null)
                return organization;

            organization = new OrganizationEntity
            {
                Name = DefaultOrganizationName,
                Inn = "000000000",
                Address = "Tashkent",
                PhoneNumber = "998000000000",
                IsActive = true
            };

            await context.Organizations.AddAsync(organization);
            await context.SaveChangesAsync();

            return organization;
        }

        private static async Task<RoleEntity> SeedDefaultRoleAsync(AppDbContext context, long organizationId)
        {
            var role = await context.Roles
                .FirstOrDefaultAsync(r => r.Name == DefaultRoleName && !r.IsDeleted);

            if (role is not null)
                return role;

            role = new RoleEntity
            {
                Name = DefaultRoleName,
                Description = "Barcha huquqlarga ega administrator roli.",
                IsActive = true
            };

            await context.Roles.AddAsync(role);
            await context.SaveChangesAsync();

            return role;
        }

        private static async Task SeedRolePermissionsAsync(AppDbContext context, RoleEntity role)
        {
            var existingPermissionNames = await context.RolePermissions
                .Where(rp => rp.RoleId == role.Id && !rp.IsDeleted)
                .Include(rp => rp.Permission)
                .Select(rp => rp.Permission!.Name)
                .ToListAsync();

            var missingNames = Permissions.All
                .Where(p => !existingPermissionNames.Contains(p))
                .ToList();

            if (missingNames.Count == 0)
                return;

            foreach (var name in missingNames)
            {
                var permission = await context.Permissions
                    .FirstOrDefaultAsync(p => p.Name == name && !p.IsDeleted);

                if (permission is null)
                {
                    permission = new PermissionEntity { Name = name };
                    await context.Permissions.AddAsync(permission);
                    await context.SaveChangesAsync();
                }

                var alreadyLinked = await context.RolePermissions
                    .AnyAsync(rp => rp.RoleId == role.Id && rp.PermissionId == permission.Id && !rp.IsDeleted);

                if (!alreadyLinked)
                {
                    await context.RolePermissions.AddAsync(new RolePermissionEntity
                    {
                        RoleId = role.Id,
                        PermissionId = permission.Id
                    });
                }
            }

            await context.SaveChangesAsync();
        }

        private static async Task SeedDefaultUserAsync(AppDbContext context, long roleId)
        {
            var user = await context.Users
                .FirstOrDefaultAsync(u => u.PhoneNumber == DefaultPhoneNumber && !u.IsDeleted);

            if (user is null)
            {
                var (hash, salt) = PasswordHelper.CreatePassword(DefaultPassword);

                user = new NaturalUserEntity
                {
                    PhoneId = DefaultPhoneId,
                    PhoneNumber = DefaultPhoneNumber,
                    Mail = DefaultMail,
                    PasswordHash = hash,
                    PasswordSalt = salt,
                    IsVerified = true,
                    IsOtpVerified = true,
                    RoleId = roleId
                };

                await context.Users.AddAsync(user);
                await context.SaveChangesAsync();
                return;
            }

            if (user.RoleId != roleId)
            {
                user.RoleId = roleId;
                await context.SaveChangesAsync();
            }
        }
    }
}
