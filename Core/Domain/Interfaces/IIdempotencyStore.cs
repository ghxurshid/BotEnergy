namespace Domain.Interfaces
{
    /// <summary>
    /// POST endpointlari uchun idempotency cache.
    /// Klient bir xil "Idempotency-Key" bilan retry qilganda — birinchi urinishning
    /// javobi qaytarilib, takroriy ish bajarilmaydi (masalan: ikki marta charge yo'q).
    /// </summary>
    public interface IIdempotencyStore
    {
        /// <summary>Cache'lanagn javobni qaytaradi yoki null (hali bajarilmagan / reserved).</summary>
        Task<IdempotencyEntry?> TryGetAsync(string key);

        /// <summary>Atomic reservation — true qaytsa, joriy request egasi.
        /// false qaytsa, parallel request hozir bajarilmoqda.</summary>
        Task<bool> TryReserveAsync(string key, TimeSpan reservation);

        Task SaveResponseAsync(string key, int statusCode, string body, TimeSpan ttl);
        Task ReleaseAsync(string key);
    }

    public sealed class IdempotencyEntry
    {
        public int StatusCode { get; set; }
        public string Body { get; set; } = string.Empty;
    }
}
