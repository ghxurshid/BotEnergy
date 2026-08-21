namespace Domain.Enums
{
    /// <summary>
    /// Inkassatsiya dalolatnomasining holati.
    ///
    /// Oqim: <c>Requested</c> (inkassator boxni ochishni so'radi) →
    /// <c>BoxOpened</c> (qurilma boxni ochdi va tasdiqladi) →
    /// <c>Confirmed</c> (inkassator sanab tasdiqladi, qurilma qoldig'i nolga tushdi).
    ///
    /// Qoldiq faqat <c>Confirmed</c> da nolga tushadi — box ochilgani o'zi pul
    /// olinganini bildirmaydi.
    /// </summary>
    public enum CashCollectionStatus
    {
        /// <summary>Box ochish so'raldi, qurilmadan tasdiq kutilmoqda.</summary>
        Requested = 0,

        /// <summary>Qurilma boxni ochdi.</summary>
        BoxOpened = 1,

        /// <summary>Inkassator pulni sanab tasdiqladi — qurilma qoldig'i nolga tushirildi.</summary>
        Confirmed = 2,

        /// <summary>Inkassator amalni bekor qildi (pul olinmadi).</summary>
        Cancelled = 3,

        /// <summary>Qurilma boxni ocha olmadi yoki javob bermadi.</summary>
        Failed = 4
    }
}
