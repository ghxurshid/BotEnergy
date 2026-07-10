namespace Domain.Constants
{
    /// <summary>
    /// Tizimdagi barcha permission nomlari.
    /// DataSeeder va API controllerlarda yagona manba sifatida ishlatiladi.
    ///
    /// Permissionlar ikki guruhga ajraladi:
    ///  - <see cref="PlatformAll"/> — Platform (Manage/Merchant) rollariga biriktiriladi.
    ///  - <see cref="CustomerAll"/> — Customer (Natural/Corporate) rollariga biriktiriladi.
    /// <see cref="All"/> — ikkalasining birlashmasi (DataSeeder permission satrlarini yaratish uchun).
    /// </summary>
    public static class Permissions
    {
        // ── AdminApi — Rol va permission boshqaruvi ───────────────────
        public const string RoleCreateRole = "Role.CreateRole";
        public const string RoleGetAll = "Role.GetAll";
        public const string RoleGetById = "Role.GetById";
        public const string RoleUpdate = "Role.Update";
        public const string RoleDelete = "Role.Delete";
        public const string RoleGetPermissions = "Role.GetPermissions";
        public const string RoleGetAllowedPermissions = "Role.GetAllowedPermissions";

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
        public const string StationAdminGetByMerchant = "StationAdmin.GetByMerchant";
        public const string StationAdminUpdate = "StationAdmin.Update";
        public const string StationAdminDelete = "StationAdmin.Delete";

        // ── AdminApi — Qurilma boshqaruvi ────────────────────────────
        public const string DeviceAdminRegister = "DeviceAdmin.Register";
        public const string DeviceAdminGetAll = "DeviceAdmin.GetAll";
        public const string DeviceAdminGetById = "DeviceAdmin.GetById";
        public const string DeviceAdminGetByStation = "DeviceAdmin.GetByStation";
        public const string DeviceAdminUpdate = "DeviceAdmin.Update";
        public const string DeviceAdminDelete = "DeviceAdmin.Delete";
        /// <summary>Expert-rejim: qurilma EEPROM qayta flash qilinganda MQTT counter'larni 0'lash.</summary>
        public const string DeviceAdminResetMqttCounters = "DeviceAdmin.ResetMqttCounters";

        // ── AdminApi — Mahsulot boshqaruvi ───────────────────────────
        public const string ProductAdminCreate = "ProductAdmin.Create";
        public const string ProductAdminGetAll = "ProductAdmin.GetAll";
        public const string ProductAdminGetByDevice = "ProductAdmin.GetByDevice";
        public const string ProductAdminGetById = "ProductAdmin.GetById";
        public const string ProductAdminGetAllowedTypes = "ProductAdmin.GetAllowedTypes";
        public const string ProductAdminUpdate = "ProductAdmin.Update";
        public const string ProductAdminDelete = "ProductAdmin.Delete";

        // ── AdminApi — Platform foydalanuvchi boshqaruvi ─────────────
        public const string UserAdminCreate = "UserAdmin.Create";
        public const string UserAdminGetAll = "UserAdmin.GetAll";
        public const string UserAdminGetById = "UserAdmin.GetById";
        public const string UserAdminSetPassword = "UserAdmin.SetPassword";
        public const string UserAdminResetPassword = "UserAdmin.ResetPassword";
        public const string UserAdminBlock = "UserAdmin.Block";
        public const string UserAdminUnblock = "UserAdmin.Unblock";
        public const string UserAdminDelete = "UserAdmin.Delete";

        // ── AdminApi — Merchant boshqaruvi ──────────────────────────
        public const string MerchantAdminRegister = "MerchantAdmin.Register";
        public const string MerchantAdminGetAll = "MerchantAdmin.GetAll";
        public const string MerchantAdminGetById = "MerchantAdmin.GetById";
        public const string MerchantAdminUpdate = "MerchantAdmin.Update";
        public const string MerchantAdminDelete = "MerchantAdmin.Delete";

        // ── Corporate foydalanuvchi boshqaruvi (Customer guruhi) ─────
        // Corporate bosh admini o'z tashkilotidagi qo'l-osti userlarni boshqaradi.
        public const string CustomerAdminCreate = "CustomerAdmin.Create";
        public const string CustomerAdminGetAll = "CustomerAdmin.GetAll";
        public const string CustomerAdminGetById = "CustomerAdmin.GetById";
        public const string CustomerAdminSetPassword = "CustomerAdmin.SetPassword";
        public const string CustomerAdminBlock = "CustomerAdmin.Block";
        public const string CustomerAdminUnblock = "CustomerAdmin.Unblock";
        public const string CustomerAdminDelete = "CustomerAdmin.Delete";

        // ── BillingApi — Balans boshqaruvi ───────────────────────────
        public const string BalanceTopUp = "Balance.TopUp";

        // ── PaymentApi — Payme orqali QR to'lov ──────────────────────
        public const string PaymentTopUpSelf = "Payment.TopUpSelf";
        public const string PaymentTopUpOrganization = "Payment.TopUpOrganization";
        public const string PaymentGetMyTransactions = "Payment.GetMyTransactions";
        public const string PaymentGetOrganizationTransactions = "Payment.GetOrganizationTransactions";

        // ── AdminApi — To'lov audit ──────────────────────────────────
        public const string PaymentAdminGetAll = "PaymentAdmin.GetAll";
        public const string PaymentAdminGetById = "PaymentAdmin.GetById";
        public const string PaymentAdminGetSteps = "PaymentAdmin.GetSteps";
        public const string PaymentAdminReverse = "PaymentAdmin.Reverse";

        // ── UserApi — Sessiya boshqaruvi ─────────────────────────────
        public const string SessionCreate = "Session.Create";
        public const string SessionClose = "Session.Close";
        public const string SessionRead = "Session.Read";
        public const string SessionHeartbeat = "Session.Heartbeat";

        // ── UserApi — Mahsulot berish jarayoni ───────────────────────
        public const string ProcessStart = "Process.Start";
        public const string ProcessStop = "Process.Stop";
        public const string ProcessPause = "Process.Pause";
        public const string ProcessResume = "Process.Resume";

        // ── UserApi — Foydalanuvchi profili ──────────────────────────
        public const string UserMe = "User.Me";
        public const string UserUpdateMe = "User.UpdateMe";
        public const string UserBootstrap = "User.Bootstrap";
        public const string DeviceConnectionGetProducts = "DeviceConnection.GetProducts";

        // ── UserApi — Foydalanish hisoboti ───────────────────────────
        public const string ReportMyUsage = "Report.MyUsage";
        public const string ReportMyUsageExport = "Report.MyUsageExport";

        // ── Tashkilot (corporate) hisoboti ───────────────────────────
        public const string OrganizationReportUsage = "OrganizationReport.Usage";
        public const string OrganizationReportUsageExport = "OrganizationReport.UsageExport";

        // ── AdminApi — Merchant savdo hisoboti ───────────────────────
        public const string MerchantReportSales = "MerchantReport.Sales";
        public const string MerchantReportSalesExport = "MerchantReport.SalesExport";

        /// <summary>
        /// Platform (Manage/Merchant) rollariga biriktirilishi mumkin bo'lgan permissionlar.
        /// </summary>
        public static readonly List<string> PlatformAll = new()
        {
            // Role
            RoleCreateRole, RoleGetAll, RoleGetById, RoleUpdate, RoleDelete,
            RoleGetPermissions, RoleGetAllowedPermissions,

            // Organization
            OrganizationAdminCreate, OrganizationAdminGetAll, OrganizationAdminGetById,
            OrganizationAdminUpdate, OrganizationAdminDelete,

            // Station
            StationAdminCreate, StationAdminGetAll, StationAdminGetById,
            StationAdminGetByMerchant, StationAdminUpdate, StationAdminDelete,

            // Device
            DeviceAdminRegister, DeviceAdminGetAll, DeviceAdminGetById,
            DeviceAdminGetByStation, DeviceAdminUpdate, DeviceAdminDelete,
            DeviceAdminResetMqttCounters,

            // Product
            ProductAdminCreate, ProductAdminGetAll, ProductAdminGetByDevice,
            ProductAdminGetById, ProductAdminGetAllowedTypes, ProductAdminUpdate,
            ProductAdminDelete,

            // Platform User Admin
            UserAdminCreate, UserAdminGetAll, UserAdminGetById,
            UserAdminSetPassword, UserAdminResetPassword,
            UserAdminBlock, UserAdminUnblock, UserAdminDelete,

            // Merchant
            MerchantAdminRegister, MerchantAdminGetAll, MerchantAdminGetById,
            MerchantAdminUpdate, MerchantAdminDelete,

            // Billing
            BalanceTopUp,

            // Payment audit
            PaymentAdminGetAll, PaymentAdminGetById,
            PaymentAdminGetSteps, PaymentAdminReverse,

            // Reports
            MerchantReportSales, MerchantReportSalesExport,
        };

        /// <summary>
        /// Customer (Natural/Corporate) rollariga biriktirilishi mumkin bo'lgan permissionlar.
        /// </summary>
        public static readonly List<string> CustomerAll = new()
        {
            // Session
            SessionCreate, SessionClose, SessionRead, SessionHeartbeat,

            // Process
            ProcessStart, ProcessStop, ProcessPause, ProcessResume,

            // Profile
            UserMe, UserUpdateMe, UserBootstrap, DeviceConnectionGetProducts,

            // Reports
            ReportMyUsage, ReportMyUsageExport,
            OrganizationReportUsage, OrganizationReportUsageExport,

            // Payment (self + organization)
            PaymentTopUpSelf, PaymentGetMyTransactions,
            PaymentTopUpOrganization, PaymentGetOrganizationTransactions,

            // Corporate sub-user management
            CustomerAdminCreate, CustomerAdminGetAll, CustomerAdminGetById,
            CustomerAdminSetPassword, CustomerAdminBlock, CustomerAdminUnblock,
            CustomerAdminDelete,
        };

        /// <summary>
        /// Barcha permissionlar (PlatformAll ∪ CustomerAll) — DataSeeder permission
        /// satrlarini yaratish uchun.
        /// </summary>
        public static readonly List<string> All =
            PlatformAll.Concat(CustomerAll).Distinct().ToList();
    }
}
