namespace Domain.Enums
{
    /// <summary>
    /// Foydalanuvchining yuqori darajadagi guruhi — tizimni ikkita mustaqil
    /// domenga ajratadi: platformani boshqaruvchilar va undan xizmat oluvchilar.
    /// </summary>
    public enum UserGroup
    {
        /// <summary>Platformani boshqaradiganlar (Manage / Merchant).</summary>
        Platform,

        /// <summary>Platforma orqali mahsulot sotib oluvchilar (Natural / Corporate).</summary>
        Customer
    }
}
