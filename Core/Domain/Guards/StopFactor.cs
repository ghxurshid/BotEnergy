namespace Domain.Guards
{
    /// <summary>
    /// <b>To'sqinlik omili (stop factor)</b> — amalni bajarishga yo'l qo'ymaydigan aniq sabab.
    ///
    /// Loyihaviy qoida: <i>har qanday holat o'zgartiruvchi amal avval o'zining barcha
    /// to'sqinlik omillarini tekshiradi; bittasi ham mavjud bo'lsa amal BOSHLANMAYDI va
    /// chaqiruvchiga aynan nima to'sqinlik qilayotgani aytiladi.</i>
    /// "Qisman bajarish" (yozuv yaratib, keyin yiqilish) mumkin emas.
    ///
    /// Uch qismdan iborat:
    ///  - <see cref="Code"/>    — mashina o'qiydigan barqaror kod (mobil ilova/qurilma shu bo'yicha
    ///                            o'z ekranini tanlaydi; matn o'zgarsa ham kod o'zgarmaydi);
    ///  - <see cref="Message"/> — foydalanuvchiga ko'rinadigan o'zbekcha sabab;
    ///  - <see cref="HttpStatus"/> — REST javob statusi (409 = holat to'sqinlik qilmoqda,
    ///                            503 = tashqi bog'liqlik yo'q, 403 = huquq yo'q, ...).
    /// </summary>
    public sealed record StopFactor(string Code, string Message, int HttpStatus)
    {
        public override string ToString() => $"{Code}: {Message}";
    }
}
