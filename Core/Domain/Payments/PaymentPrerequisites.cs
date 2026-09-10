namespace Domain.Payments
{
    /// <summary>
    /// Mijoz shu usul bilan to'lay olishi uchun nima kerakligi — payment sheet'ni chizish
    /// uchun YAGONA manba. Strategiya o'zi to'ldiradi; sirt (controller/servis) usul nomiga
    /// qarab <c>if</c> yozmaydi.
    ///
    /// Masalan Subscribe uchun: <c>RequiresCard=true</c> va tasdiqlangan karta bo'lmasa
    /// <c>IsReady=false</c> — ilova aynan shu joyda "karta qo'shish" formasini ochadi.
    /// </summary>
    /// <param name="RequiresCard">Saqlangan karta kerak (mijoz kartalar orasidan tanlaydi).</param>
    /// <param name="HasUsableCard">Tasdiqlangan, to'lovga yaroqli karta bormi.</param>
    /// <param name="SuggestedCardId">Karta ko'rsatilmasa ishlatiladigan karta (asosiy).</param>
    /// <param name="RequiresPhone">Chek telefonga yuboriladi — raqam kerak.</param>
    /// <param name="Phone">Aniqlangan raqam (profil yoki so'rovdan), maskalanmagan holda mijozning o'ziga.</param>
    /// <param name="RequiresCheckout">Mijoz tashqi havola/QR orqali to'lovni yakunlaydi.</param>
    /// <param name="IsReady">Hozir to'lov yaratish mumkinmi (hamma shart bajarilgan).</param>
    /// <param name="MissingRequirement">IsReady=false bo'lsa — mijozga aytiladigan aniq sabab.</param>
    /// <param name="Hint">Usul qanday ishlashini tushuntiruvchi qisqa matn.</param>
    public sealed record PaymentPrerequisites(
        bool RequiresCard = false,
        bool HasUsableCard = false,
        long? SuggestedCardId = null,
        bool RequiresPhone = false,
        string? Phone = null,
        bool RequiresCheckout = false,
        bool IsReady = true,
        string? MissingRequirement = null,
        string Hint = "")
    {
        public static PaymentPrerequisites Ready(string hint) => new(Hint: hint);
    }
}
