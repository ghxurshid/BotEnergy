using Domain.Entities.BaseEntity;
using Domain.Enums;

namespace Domain.Entities
{
    /// <summary>
    /// Sessiya ichidagi mahsulot berish jarayoni.
    /// Snapshot maydonlar (ProductName, PricePerUnit, Unit) — keyinchalik mahsulot o'zgarsa ham
    /// tarixiy hisobot to'g'ri bo'lishi uchun saqlanadi.
    /// </summary>
    public class ProductProcessEntity : Entity
    {
        public long SessionId { get; set; }
        public SessionEntity? Session { get; set; }

        public long ProductId { get; set; }
        public ProductEntity? Product { get; set; }

        // Snapshot maydonlar
        public string ProductName { get; set; } = string.Empty;
        public decimal PricePerUnit { get; set; }
        public UnitType Unit { get; set; }

        public decimal RequestedAmount { get; set; }
        public decimal GivenAmount { get; set; }

        public ProcessStatus Status { get; set; } = ProcessStatus.Started;
        public ProcessEndReason? EndReason { get; set; }

        public DateTime StartedAt { get; set; } = DateTime.Now;
        public DateTime? PausedAt { get; set; }
        public DateTime? EndedAt { get; set; }

        /// <summary>
        /// Balans yechib bo'linganmi — double-deduction race holatining oldini olish uchun.
        /// </summary>
        public bool IsBalanceDeducted { get; set; }

        /// <summary>
        /// Telemetry duplicate-ni aniqlash uchun ishlatiladi.
        /// Qurilma har telemetry payloadida monoton o'sib boruvchi sequence yuboradi.
        /// </summary>
        public long LastTelemetrySequence { get; set; }

        /// <summary>
        /// Optimistic concurrency control. EFCore design da [Timestamp] sifatida sozlanadi.
        /// </summary>
        public uint RowVersion { get; set; }
    }
}
