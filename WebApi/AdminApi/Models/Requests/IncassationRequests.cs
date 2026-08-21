namespace AdminApi.Models.Requests
{
    /// <summary>Qurilma boxini ochish so'rovi.</summary>
    public class RequestBoxOpenRequest
    {
        public long DeviceId { get; set; }
    }

    /// <summary>Inkassatsiyani tasdiqlash — sanalgan summa bilan.</summary>
    public class ConfirmCollectionRequest
    {
        public long CollectionId { get; set; }

        /// <summary>Inkassator qo'lda sanagan summa (UZS). Server qoldig'idan farq qilishi mumkin.</summary>
        public decimal CountedAmount { get; set; }

        public string? Notes { get; set; }
    }

    /// <summary>Boshlangan inkassatsiyani bekor qilish (pul olinmadi).</summary>
    public class CancelCollectionRequest
    {
        public long CollectionId { get; set; }
        public string? Notes { get; set; }
    }
}
