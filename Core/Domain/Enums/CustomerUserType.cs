namespace Domain.Enums
{
    /// <summary>
    /// Customer guruhidagi foydalanuvchining subtipi.
    /// </summary>
    public enum CustomerUserType
    {
        /// <summary>Jismoniy shaxs — smartfon app orqali o'zi ro'yxatdan o'tadi, o'z balansi bor.</summary>
        Natural = 0,

        /// <summary>Tashkilot xodimi — admini Platform/Manage tomonidan yaratiladi,
        /// hammasi bitta tashkilot balansidan foydalanadi.</summary>
        Corporate = 1
    }
}
