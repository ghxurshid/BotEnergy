namespace Domain.Dtos.Report
{
    /// <summary>
    /// Hisobot kimga tegishli bo'lishini bildiruvchi scope.
    /// Pattern matching orqali repository tegishli predikatga aylantiradi.
    /// </summary>
    public abstract record UsageReportScope
    {
        private UsageReportScope() { }

        /// <summary>Bitta foydalanuvchining shaxsiy iste'moli (Natural yoki Legal user).</summary>
        public sealed record User(long UserId) : UsageReportScope;

        /// <summary>Yuridik tashkilotning barcha xodimlari iste'moli.</summary>
        public sealed record Organization(long OrganizationId) : UsageReportScope;

        /// <summary>Merchant stansiyalarida ko'rsatilgan xizmat.
        /// <paramref name="StationId"/> = null bo'lsa — barcha stansiyalar.</summary>
        public sealed record Merchant(long MerchantId, long? StationId) : UsageReportScope;
    }
}
