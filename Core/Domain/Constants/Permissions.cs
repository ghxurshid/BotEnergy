namespace Domain.Constants
{
    /// <summary>
    /// Tizimdagi barcha permission nomlari.
    /// DataSeeder va API controllerlarda yagona manba sifatida ishlatiladi.
    /// </summary>
    public static class Permissions
    {
        // ── AdminApi — Rol va permission boshqaruvi ───────────────────
        public const string RoleCreateRole = "Role.CreateRole";
        public const string RoleGetAll = "Role.GetAll";
        public const string RoleAddPermission = "Role.AddPermission";
        public const string RoleRemovePermission = "Role.RemovePermission";
        public const string RoleAssignToUser = "Role.AssignToUser";
        public const string RoleGetPermissions = "Role.GetPermissions";

        // ── AdminApi — Tashkilot boshqaruvi ──────────────────────────
        public const string OrganizationAdminCreate = "OrganizationAdmin.Create";
        public const string OrganizationAdminGetAll = "OrganizationAdmin.GetAll";
        public const string OrganizationAdminGetById = "OrganizationAdmin.GetById";
        public const string OrganizationAdminUpdate = "OrganizationAdmin.Update";
        public const string OrganizationAdminDelete = "OrganizationAdmin.Delete";

        // ── AdminApi — Stansiya boshqaruvi ───────────────────────────
        public const string StationAdminCreate = "StationAdmin.Create";
        public const string StationAdminGetAll = "StationAdmin.GetAll";
        public const string StationAdminGetById = "StationAdmin.GetById";
        public const string StationAdminGetByOrganization = "StationAdmin.GetByOrganization";
        public const string StationAdminUpdate = "StationAdmin.Update";
        public const string StationAdminDelete = "StationAdmin.Delete";

        // ── AdminApi — Qurilma boshqaruvi ────────────────────────────
        public const string DeviceAdminRegister = "DeviceAdmin.Register";
        public const string DeviceAdminGetAll = "DeviceAdmin.GetAll";
        public const string DeviceAdminGetById = "DeviceAdmin.GetById";
        public const string DeviceAdminGetByStation = "DeviceAdmin.GetByStation";
        public const string DeviceAdminUpdate = "DeviceAdmin.Update";
        public const string DeviceAdminDelete = "DeviceAdmin.Delete";

        // ── AdminApi — Mahsulot boshqaruvi ───────────────────────────
        public const string ProductAdminCreate = "ProductAdmin.Create";
        public const string ProductAdminGetAllowedTypes = "ProductAdmin.GetAllowedTypes";

        // ── AdminApi — Foydalanuvchi boshqaruvi ──────────────────────
        public const string UserAdminGetAll = "UserAdmin.GetAll";
        public const string UserAdminGetById = "UserAdmin.GetById";
        public const string UserAdminBlock = "UserAdmin.Block";
        public const string UserAdminUnblock = "UserAdmin.Unblock";
        public const string UserAdminDelete = "UserAdmin.Delete";

        // ── AdminApi — Mijoz boshqaruvi ──────────────────────────────
        public const string ClientAdminRegister = "ClientAdmin.Register";
        public const string ClientAdminGetAll = "ClientAdmin.GetAll";
        public const string ClientAdminGetById = "ClientAdmin.GetById";
        public const string ClientAdminUpdate = "ClientAdmin.Update";
        public const string ClientAdminDelete = "ClientAdmin.Delete";

        // ── AdminApi — Yuridik foydalanuvchi ─────────────────────────
        public const string YuridikAdminCreate = "YuridikAdmin.Create";

        // ── BillingApi — Balans boshqaruvi ───────────────────────────
        public const string BalanceGetMyBalance = "Balance.GetMyBalance";
        public const string BalanceTopUp = "Balance.TopUp";

        // ── UserApi — Sessiya boshqaruvi ─────────────────────────────
        public const string SessionCreate = "Session.Create";
        public const string SessionSetQuantity = "Session.SetQuantity";
        public const string SessionClose = "Session.Close";

        // ── UserApi — Foydalanuvchi profili ──────────────────────────
        public const string UserMe = "User.Me";
        public const string UserUpdateMe = "User.UpdateMe";
        public const string DeviceConnectionGetProducts = "DeviceConnection.GetProducts";

        /// <summary>
        /// Barcha permissionlar ro'yxati — DataSeeder uchun.
        /// </summary>
        public static readonly List<string> All = new()
        {
            // Role
            RoleCreateRole, RoleGetAll, RoleAddPermission,
            RoleRemovePermission, RoleAssignToUser, RoleGetPermissions,

            // Organization
            OrganizationAdminCreate, OrganizationAdminGetAll, OrganizationAdminGetById,
            OrganizationAdminUpdate, OrganizationAdminDelete,

            // Station
            StationAdminCreate, StationAdminGetAll, StationAdminGetById,
            StationAdminGetByOrganization, StationAdminUpdate, StationAdminDelete,

            // Device
            DeviceAdminRegister, DeviceAdminGetAll, DeviceAdminGetById,
            DeviceAdminGetByStation, DeviceAdminUpdate, DeviceAdminDelete,

            // Product
            ProductAdminCreate, ProductAdminGetAllowedTypes,

            // User Admin
            UserAdminGetAll, UserAdminGetById, UserAdminBlock,
            UserAdminUnblock, UserAdminDelete,

            // Client
            ClientAdminRegister, ClientAdminGetAll, ClientAdminGetById,
            ClientAdminUpdate, ClientAdminDelete,

            // Yuridik
            YuridikAdminCreate,

            // Billing
            BalanceGetMyBalance, BalanceTopUp,

            // Session
            SessionCreate, SessionSetQuantity, SessionClose,

            // User Profile
            UserMe, UserUpdateMe, DeviceConnectionGetProducts,
        };
    }
}
