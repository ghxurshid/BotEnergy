using Domain.Auth;
using Domain.Dtos.Base;
using Domain.Dtos.Cash;
using Domain.Enums;

namespace Domain.Interfaces
{
    /// <summary>
    /// Inkassator ilovasining backend mantiqi: qurilmalardagi naqd pulni ko'rish,
    /// boxni ochtirish va olingan summani tasdiqlash.
    ///
    /// Barcha amallar <see cref="AccessScope"/> bilan cheklanadi — merchant inkassatori
    /// faqat o'z merchantining qurilmalarini ko'radi va ochadi.
    /// </summary>
    public interface IIncassationService
    {
        /// <summary>Naqd qoldiq va koordinata bilan qurilmalar (xarita va ro'yxat uchun).</summary>
        Task<GenericDto<List<IncassationDeviceDto>>> GetDevicesAsync(AccessScope scope);

        /// <summary>
        /// Boxni ochishni so'raydi: dalolatnoma yaratadi va qurilmaga buyruq yuboradi.
        /// Qoldiq shu paytda <c>ExpectedAmount</c> sifatida muzlatiladi.
        /// </summary>
        Task<GenericDto<CashCollectionDto>> RequestOpenAsync(AccessScope scope, long deviceId);

        /// <summary>
        /// Qurilma boxni ochganini tasdiqladi (MQTT <c>cash.box.opened</c> handler'idan chaqiriladi).
        /// Qoldiq bu yerda NOLGA TUSHMAYDI — box ochilgani pul olinganini bildirmaydi.
        /// </summary>
        Task MarkBoxOpenedAsync(string serialNumber, long collectionId, CancellationToken ct = default);

        /// <summary>
        /// Inkassator pulni sanab tasdiqladi: qurilma qoldig'i nolga tushadi,
        /// sanalgan va kutilgan summa farqi yozib qo'yiladi.
        /// </summary>
        Task<GenericDto<CashCollectionDto>> ConfirmAsync(
            AccessScope scope, long collectionId, decimal countedAmount, string? notes);

        /// <summary>Amalni bekor qilish — box ochilgan bo'lsa ham pul olinmagan bo'lsa.</summary>
        Task<GenericDto<CashCollectionDto>> CancelAsync(AccessScope scope, long collectionId, string? notes);

        /// <summary>Audit tarixi.</summary>
        Task<GenericDto<PagedResult<CashCollectionDto>>> GetHistoryAsync(
            AccessScope scope, PaginationParams param, long? deviceId);

        /// <summary>
        /// Naqd → karta sessiyalari (admin auditi). <paramref name="status"/> bilan
        /// <c>PayoutFailed</c> ni filtrlash — qo'lda hal qilishni talab qiladigan holatlar.
        /// </summary>
        Task<GenericDto<PagedResult<CashSessionListDto>>> GetCashSessionsAsync(
            AccessScope scope, PaginationParams param, CashSessionStatus? status);
    }
}
