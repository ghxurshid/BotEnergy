using Application.Helpers;
using Domain.Constants;
using Domain.Entities;
using Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Persistence.Context;

namespace Persistence.Seed
{
    public static class DataSeeder
    {
        private const string ManageRoleName = "SuperAdmin";
        private const string NaturalRoleName = "Customer";
        private const string DefaultPhoneNumber = "998901234567";
        private const string DefaultPassword = "Admin@123";
        private const string DefaultMail = "admin@botenergy.uz";
        private const string DefaultPhoneId = "default-admin-device";

        public static async Task SeedAsync(AppDbContext context)
        {
            await EnsurePermissionRowsAsync(context);

            var manageRole = await SeedManageRoleAsync(context);
            await SeedManageUserAsync(context, manageRole.Id);

            await SeedNaturalRoleAsync(context);
        }

        /// <summary>Barcha permission satrlari (Permissions.All) mavjudligini ta'minlaydi.</summary>
        private static async Task EnsurePermissionRowsAsync(AppDbContext context)
        {
            var existing = await context.Permissions
                .Select(p => p.Name)
                .ToListAsync();

            var missing = Permissions.All.Where(p => !existing.Contains(p)).ToList();
            if (missing.Count == 0)
                return;

            foreach (var name in missing)
                await context.Permissions.AddAsync(new PermissionEntity { Name = name });

            await context.SaveChangesAsync();
        }

        private static async Task<PlatformRoleEntity> SeedManageRoleAsync(AppDbContext context)
        {
            var role = await context.PlatformRoles
                .FirstOrDefaultAsync(r => r.Name == ManageRoleName && !r.IsDeleted);

            if (role is null)
            {
                role = new PlatformRoleEntity
                {
                    Name = ManageRoleName,
                    Description = "Platforma darajasidagi barcha huquqlarga ega Manage administrator roli.",
                    IsActive = true,
                    MerchantId = null
                };
                await context.PlatformRoles.AddAsync(role);
                await context.SaveChangesAsync();
            }

            await SyncRolePermissionsAsync(
                context, role.Id, Permissions.All,
                existing: await context.PlatformRolePermissions
                    .Where(rp => rp.RoleId == role.Id && !rp.IsDeleted)
                    .Include(rp => rp.Permission)
                    .Select(rp => rp.Permission!.Name)
                    .ToListAsync(),
                factory: pid => new PlatformRolePermissionEntity { RoleId = role.Id, PermissionId = pid },
                add: e => context.PlatformRolePermissions.Add(e));

            return role;
        }

        private static async Task SeedNaturalRoleAsync(AppDbContext context)
        {
            var role = await context.CustomerRoles
                .FirstOrDefaultAsync(r => r.Name == NaturalRoleName && r.OrganizationId == null && !r.IsDeleted);

            if (role is null)
            {
                role = new CustomerRoleEntity
                {
                    Name = NaturalRoleName,
                    Description = "Jismoniy foydalanuvchilar uchun standart rol (self-register).",
                    IsActive = true,
                    OrganizationId = null
                };
                await context.CustomerRoles.AddAsync(role);
                await context.SaveChangesAsync();
            }

            await SyncRolePermissionsAsync(
                context, role.Id, PermissionScopes.NaturalAllowed.ToList(),
                existing: await context.CustomerRolePermissions
                    .Where(rp => rp.RoleId == role.Id && !rp.IsDeleted)
                    .Include(rp => rp.Permission)
                    .Select(rp => rp.Permission!.Name)
                    .ToListAsync(),
                factory: pid => new CustomerRolePermissionEntity { RoleId = role.Id, PermissionId = pid },
                add: e => context.CustomerRolePermissions.Add(e));
        }

        /// <summary>Rolga yetishmayotgan permissionlarni biriktiradi (umumiy yordamchi).</summary>
        private static async Task SyncRolePermissionsAsync<TLink>(
            AppDbContext context,
            long roleId,
            IReadOnlyCollection<string> desiredNames,
            List<string> existing,
            Func<long, TLink> factory,
            Action<TLink> add)
            where TLink : class
        {
            var missingNames = desiredNames.Where(n => !existing.Contains(n)).ToList();
            if (missingNames.Count == 0)
                return;

            var idByName = await context.Permissions
                .Where(p => missingNames.Contains(p.Name))
                .ToDictionaryAsync(p => p.Name, p => p.Id);

            foreach (var name in missingNames)
            {
                if (idByName.TryGetValue(name, out var pid))
                    add(factory(pid));
            }

            await context.SaveChangesAsync();
        }

        private static async Task SeedManageUserAsync(AppDbContext context, long roleId)
        {
            var user = await context.PlatformUsers
                .FirstOrDefaultAsync(u => u.PhoneNumber == DefaultPhoneNumber && !u.IsDeleted);

            if (user is null)
            {
                var (hash, salt) = PasswordHelper.CreatePassword(DefaultPassword);

                user = new PlatformUserEntity
                {
                    Type = PlatformUserType.Manage,
                    PhoneId = DefaultPhoneId,
                    PhoneNumber = DefaultPhoneNumber,
                    Mail = DefaultMail,
                    PasswordHash = hash,
                    PasswordSalt = salt,
                    IsVerified = true,
                    IsOtpVerified = true,
                    RoleId = roleId
                };

                await context.PlatformUsers.AddAsync(user);
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
