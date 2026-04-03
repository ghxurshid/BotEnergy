namespace AdminApi.Filters.PermissionFilters
{
    public static class Permissions
    {
        // RoleController
        public const string RoleCreateRole = "Role.CreateRole";
        public const string RoleGetAll = "Role.GetAll";
        public const string RoleAddPermission = "Role.AddPermission";
        public const string RoleRemovePermission = "Role.RemovePermission";
        public const string RoleAssignToUser = "Role.AssignToUser";
        public const string RoleGetPermissions = "Role.GetPermissions";

        // OrganizationAdminController
        public const string OrganizationAdminCreate = "OrganizationAdmin.Create";
        public const string OrganizationAdminGetAll = "OrganizationAdmin.GetAll";
        public const string OrganizationAdminGetById = "OrganizationAdmin.GetById";
        public const string OrganizationAdminUpdate = "OrganizationAdmin.Update";
        public const string OrganizationAdminDelete = "OrganizationAdmin.Delete";

        // StationAdminController
        public const string StationAdminCreate = "StationAdmin.Create";
        public const string StationAdminGetAll = "StationAdmin.GetAll";
        public const string StationAdminGetById = "StationAdmin.GetById";
        public const string StationAdminGetByOrganization = "StationAdmin.GetByOrganization";
        public const string StationAdminUpdate = "StationAdmin.Update";
        public const string StationAdminDelete = "StationAdmin.Delete";

        // DeviceAdminController
        public const string DeviceAdminRegister = "DeviceAdmin.Register";
        public const string DeviceAdminGetAll = "DeviceAdmin.GetAll";
        public const string DeviceAdminGetById = "DeviceAdmin.GetById";
        public const string DeviceAdminGetByStation = "DeviceAdmin.GetByStation";
        public const string DeviceAdminUpdate = "DeviceAdmin.Update";
        public const string DeviceAdminDelete = "DeviceAdmin.Delete";

        // ClientAdminController
        public const string ClientAdminRegister = "ClientAdmin.Register";
        public const string ClientAdminGetAll = "ClientAdmin.GetAll";
        public const string ClientAdminGetById = "ClientAdmin.GetById";
        public const string ClientAdminUpdate = "ClientAdmin.Update";
        public const string ClientAdminDelete = "ClientAdmin.Delete";

        // UserAdminController
        public const string UserAdminGetAll = "UserAdmin.GetAll";
        public const string UserAdminGetById = "UserAdmin.GetById";
        public const string UserAdminBlock = "UserAdmin.Block";
        public const string UserAdminUnblock = "UserAdmin.Unblock";
        public const string UserAdminDelete = "UserAdmin.Delete";

        // ProductAdminController
        public const string ProductAdminGetAllowedTypes = "ProductAdmin.GetAllowedTypes";
        public const string ProductAdminCreate = "ProductAdmin.Create";

        // YuridikAdminController
        public const string YuridikAdminCreate = "YuridikAdmin.Create";
    }
}
