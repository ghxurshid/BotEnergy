using Domain.Enums;

namespace Domain.Dtos.Cash
{
    /// <summary>
    /// Inkassator ilovasining xarita/ro'yxat elementi: qurilmada qancha naqd bor
    /// va u qayerda joylashgan.
    /// </summary>
    public sealed class IncassationDeviceDto
    {
        public long DeviceId { get; set; }
        public string SerialNumber { get; set; } = string.Empty;
        public string? Model { get; set; }

        public long StationId { get; set; }
        public string StationName { get; set; } = string.Empty;
        public string Address { get; set; } = string.Empty;

        /// <summary>Kenglik (latitude) — PostGIS Point'ning Y koordinatasi.</summary>
        public double Latitude { get; set; }

        /// <summary>Uzunlik (longitude) — PostGIS Point'ning X koordinatasi.</summary>
        public double Longitude { get; set; }

        public decimal CashBalance { get; set; }
        public DateTime? CashLastCollectedAt { get; set; }

        public bool IsOnline { get; set; }
        public DateTime? LastSeenAt { get; set; }

        /// <summary>
        /// Qurilmada tugallanmagan inkassatsiya bormi. <c>true</c> bo'lsa ilova
        /// "Boxni ochish" o'rniga davom etayotgan amalni ko'rsatadi.
        /// </summary>
        public bool HasOpenCollection { get; set; }

        public long? OpenCollectionId { get; set; }
    }

    /// <summary>
    /// Admin ro'yxati uchun naqd → karta sessiyasi. Asosiy foydalanish holati —
    /// <c>PayoutFailed</c> sessiyalarni topib qo'lda hal qilish.
    /// Karta faqat maskalangan ko'rinishda; token hech qachon chiqarilmaydi.
    /// </summary>
    public sealed class CashSessionListDto
    {
        public long Id { get; set; }
        public long DeviceId { get; set; }
        public string SerialNumber { get; set; } = string.Empty;
        public string CardMasked { get; set; } = string.Empty;

        public CashSessionStatus Status { get; set; }
        public decimal AcceptedAmount { get; set; }
        public int BillCount { get; set; }

        public string? PayoutReference { get; set; }
        public string? FailureReason { get; set; }

        public int AttemptCount { get; set; }
        public DateTime? NextAttemptAt { get; set; }

        public DateTime CreatedDate { get; set; }
        public DateTime? CompletedAt { get; set; }
    }

    /// <summary>Inkassatsiya dalolatnomasi (so'rov natijasi va audit tarixi elementi).</summary>
    public sealed class CashCollectionDto
    {
        public long Id { get; set; }
        public long DeviceId { get; set; }
        public string SerialNumber { get; set; } = string.Empty;

        public CashCollectionStatus Status { get; set; }

        public decimal ExpectedAmount { get; set; }
        public decimal? CountedAmount { get; set; }

        /// <summary>Sanalgan va kutilgan summa farqi (tasdiqlangandan keyin to'ldiriladi).</summary>
        public decimal? Difference => CountedAmount is null ? null : CountedAmount - ExpectedAmount;

        public long IncassatorUserId { get; set; }

        public DateTime RequestedAt { get; set; }
        public DateTime? BoxOpenedAt { get; set; }
        public DateTime? ConfirmedAt { get; set; }

        public string? Notes { get; set; }
    }
}
