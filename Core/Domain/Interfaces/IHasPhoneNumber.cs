namespace Domain.Interfaces
{
    /// <summary>
    /// Telefon raqam saqlaydigan entity'lar. AppDbContext.SaveChanges guard shu interfeys
    /// orqali insert/update paytida raqamni normalizatsiya qilib, canonical formatni kafolatlaydi.
    /// </summary>
    public interface IHasPhoneNumber
    {
        string PhoneNumber { get; set; }
    }
}
