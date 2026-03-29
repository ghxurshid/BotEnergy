using Application.Helpers;
using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Persistence.Context;

namespace Persistence.Seed
{
    public static class DataSeeder
    {
        private const string DefaultOrganizationName = "BotEnergy System";
        private const string DefaultRoleName = "SuperAdmin";
        private const string DefaultPhoneNumber = "+998901234567";
        private const string DefaultPassword = "Admin@123";
        private const string DefaultMail = "admin@botenergy.uz";
        private const string DefaultPhoneId = "default-admin-device";

        private static readonly List<string> AllPermissions = new()
        {
            // AdminApi — Rol va permission boshqaruvi
            "Role.CreateRole",
            "Role.GetAll",
            "Role.AddPermission",
            "Role.RemovePermission",
            "Role.AssignToUser",
            "Role.GetPermissions",

            // AdminApi — Admin boshqaruvi
            "ClientAdmin.Register",
            "DeviceAdmin.Register",
            "DeviceAdmin.ChangeStatus",
            "YuridikAdmin.Create",

            // UserApi — Qurilma ulanish
            "DeviceConnection.Connect",
            "DeviceConnection.Disconnect",

            // UserApi — Foydalanuvchi profili (SkipPermissionCheck bo'lsa ham qo'shiladi)
            "User.Me",
            "User.UpdateMe",
        };

        public static async Task SeedAsync(AppDbContext context)
        {
            await context.Database.MigrateAsync();

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
                IsActive = true,
                OrganizationId = organizationId
            };

            await context.Roles.AddAsync(role);
            await context.SaveChangesAsync();

            return role;
        }

        private static async Task SeedRolePermissionsAsync(AppDbContext context, RoleEntity role)
        {
            var existingPermissions = await context.RolePermissions
                .Where(rp => rp.RoleId == role.Id && !rp.IsDeleted)
                .Select(rp => rp.Permission)
                .ToListAsync();

            var missing = AllPermissions
                .Where(p => !existingPermissions.Contains(p))
                .ToList();

            if (missing.Count == 0)
                return;

            var newPermissions = missing.Select(p => new RolePermissionEntity
            {
                RoleId = role.Id,
                Permission = p
            });

            await context.RolePermissions.AddRangeAsync(newPermissions);
            await context.SaveChangesAsync();
        }

        private static async Task SeedDefaultUserAsync(AppDbContext context, long roleId)
        {
            var user = await context.Users
                .FirstOrDefaultAsync(u => u.PhoneNumber == DefaultPhoneNumber && !u.IsDeleted);

            if (user is null)
            {
                var (hash, salt) = PasswordHelper.CreatePassword(DefaultPassword);

                user = new UserEntity
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
