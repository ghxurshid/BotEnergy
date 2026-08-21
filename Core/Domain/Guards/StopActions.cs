namespace Domain.Guards
{
    /// <summary>
    /// To'siq tekshiruvi qo'llanadigan amallarning nomlari — log va diagnostikada
    /// "qaysi amal to'sildi" savoliga bir xil javob bo'lishi uchun.
    /// Nomlash: <c>{Obyekt}.{Amal}</c>.
    /// </summary>
    public static class StopActions
    {
        // Sessiya / jarayon
        public const string SessionCreate = "Session.Create";
        public const string SessionClose = "Session.Close";
        public const string SessionConnect = "Session.Connect";
        public const string SessionHeartbeat = "Session.Heartbeat";
        public const string ProcessStart = "Process.Start";
        public const string ProcessStop = "Process.Stop";
        public const string ProcessPause = "Process.Pause";
        public const string ProcessResume = "Process.Resume";

        // Naqd pul
        public const string CashSessionOpen = "Cash.SessionOpen";
        public const string CashBillAdd = "Cash.BillAdd";
        public const string CashCommit = "Cash.Commit";
        public const string CashCancel = "Cash.Cancel";
        public const string CashRetry = "Cash.RetryPayout";

        // Inkassatsiya
        public const string CashBoxOpen = "Incassation.RequestOpen";
        public const string CashCollectionConfirm = "Incassation.Confirm";
        public const string CashCollectionCancel = "Incassation.Cancel";

        // Qurilma / stansiya / mahsulot
        public const string DeviceRegister = "Device.Register";
        public const string DeviceUpdate = "Device.Update";
        public const string DeviceDelete = "Device.Delete";
        public const string StationCreate = "Station.Create";
        public const string StationUpdate = "Station.Update";
        public const string StationDelete = "Station.Delete";
        public const string ProductCreate = "Product.Create";
        public const string ProductUpdate = "Product.Update";
        public const string ProductDelete = "Product.Delete";

        // Merchant / tashkilot
        public const string MerchantCreate = "Merchant.Create";
        public const string MerchantUpdate = "Merchant.Update";
        public const string MerchantDelete = "Merchant.Delete";
        public const string MerchantSetPayme = "Merchant.SetPaymeCredentials";
        public const string OrganizationUpdate = "Organization.Update";
        public const string OrganizationDelete = "Organization.Delete";

        // Foydalanuvchi / rol
        public const string UserCreate = "User.Create";
        public const string UserBlock = "User.Block";
        public const string UserUnblock = "User.Unblock";
        public const string UserDelete = "User.Delete";
        public const string UserSetPassword = "User.SetPassword";
        public const string UserResetPassword = "User.ResetPassword";
        public const string RoleCreate = "Role.Create";
        public const string RoleUpdate = "Role.Update";
        public const string RoleDelete = "Role.Delete";

        // Pul harakati
        public const string BalanceTopUp = "Balance.TopUp";
        public const string HoldInvoiceCreate = "HoldInvoice.Create";
        public const string HoldInvoiceCancel = "HoldInvoice.Cancel";
        public const string PaymentPay = "Payment.Pay";
    }
}
