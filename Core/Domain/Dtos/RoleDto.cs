using Domain.Enums;

namespace Domain.Dtos
{
    public class CreateRoleDto
    {
        public required string Name { get; set; }
        public string? Description { get; set; }
        public bool IsActive { get; set; } = true;

        /// <summary>Platform rol uchun: null = Manage (global) rol; set = merchant scope roli.</summary>
        public long? MerchantId { get; set; }

        /// <summary>Customer rol uchun: null = global Natural rol; set = corporate org roli.</summary>
        public long? OrganizationId { get; set; }

        public List<long>? PermissionIds { get; set; }
    }

    public class UpdateRoleDto
    {
        public string? Name { get; set; }
        public string? Description { get; set; }
        public bool? IsActive { get; set; }
        public List<long>? PermissionIds { get; set; }
    }

    public class CreateRoleResultDto
    {
        public required string ResultMessage { get; set; }
        public long RoleId { get; set; }
    }

    public class RoleItemDto
    {
        public long Id { get; set; }
        public required string Name { get; set; }
        public string? Description { get; set; }
        public bool IsActive { get; set; }
        /// <summary>Rolning aniq turi: PlatformManage / PlatformMerchant / CustomerNatural / CustomerCorporate.</summary>
        public string Kind { get; set; } = string.Empty;
        public long? OrganizationId { get; set; }
        public long? MerchantId { get; set; }
        public List<string> Permissions { get; set; } = new();
    }

    public class GetRolesResultDto
    {
        public List<RoleItemDto> Roles { get; set; } = new();
    }

    public class RoleResultDto
    {
        public long Id { get; set; }
        public required string ResultMessage { get; set; }
    }

    public class GetRolePermissionsResultDto
    {
        public long RoleId { get; set; }
        public required string RoleName { get; set; }
        public List<string> Permissions { get; set; } = new();
    }

    public class AllowedPermissionDto
    {
        public long Id { get; set; }
        public required string Name { get; set; }
    }

    public class GetAllowedPermissionsResultDto
    {
        public RoleKind Kind { get; set; }
        public List<AllowedPermissionDto> Permissions { get; set; } = new();
    }
}
