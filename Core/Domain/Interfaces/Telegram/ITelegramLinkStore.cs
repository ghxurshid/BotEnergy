namespace Domain.Interfaces.Telegram
{
    /// <summary>
    /// Ilova "botga o'tish" havolasini ochganda beriladigan bir martalik token ombori.
    ///
    /// Token ichida qaysi profil (userId) uchun havola yaratilgani yoziladi — bot
    /// <c>/start &lt;token&gt;</c> ni olganda serverga aynan shu profil kerakligini biladi.
    /// Token qisqa muddatli va bir martalik; DB'ga yozilmaydi.
    /// </summary>
    public interface ITelegramLinkStore
    {
        Task SetAsync(string token, long userId, TimeSpan ttl);

        /// <summary>Tokenni atomik iste'mol qiladi (ikkinchi marta null qaytadi).</summary>
        Task<long?> ConsumeAsync(string token);
    }
}
