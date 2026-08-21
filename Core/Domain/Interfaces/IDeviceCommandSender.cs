namespace Domain.Interfaces
{
    /// <summary>
    /// Qurilmaga buyruq yuborish — MQTT ulanishi BO'LMAGAN servislar uchun ko'prik.
    ///
    /// MQTT faqat SessionApi process'ida yashaydi (arxitektura invarianti), lekin
    /// inkassatsiya endpointlari AdminApi'da. Shu sabab ikkita implementatsiya bor:
    ///  - SessionApi  → <c>LocalDeviceCommandSender</c>: to'g'ridan-to'g'ri IDeviceCommandPublisher;
    ///  - boshqa API  → <c>HttpDeviceCommandSender</c>: SessionApi'ning internal endpointiga
    ///    localhost orqali HTTP (X-Internal-Secret bilan).
    ///
    /// Har bir API o'zining implementatsiyasini Program.cs da ro'yxatdan o'tkazadi —
    /// shu sababli bu interfeys shared <c>RegisterServices</c> ga qo'yilmaydi.
    /// </summary>
    public interface IDeviceCommandSender
    {
        /// <summary>
        /// Naqd boxni ochish buyrug'i.
        /// </summary>
        /// <returns>
        /// <c>true</c> — buyruq qurilmaga uzatildi. <c>false</c> — uzatib bo'lmadi
        /// (SessionApi javob bermadi va h.k.); chaqiruvchi dalolatnomani <c>Failed</c> qiladi.
        /// </returns>
        Task<bool> SendCashBoxOpenAsync(string serialNumber, long collectionId, CancellationToken ct = default);
    }
}
