namespace Domain.Exceptions
{
    /// <summary>
    /// Bir xil Idempotency-Key bilan ikki so'rov bir vaqtda DB'ga yetib kelganda otiladi
    /// (odatda IdempotencyFilter Redis rezervatsiyasi buni oldinroq to'sadi; bu — Redis
    /// tushib qolgan yoki instansiyalar orasidagi poyga uchun oxirgi himoya).
    ///
    /// Chaqiruvchi buni "takroriy so'rov" deb qabul qilib, mavjud to'lovni qaytarishi kerak —
    /// provider IKKINCHI marta chaqirilmasligi shart.
    /// </summary>
    public sealed class DuplicateIdempotencyKeyException : Exception
    {
        public string IdempotencyKey { get; }

        public DuplicateIdempotencyKeyException(string idempotencyKey)
            : base($"Idempotency-Key '{idempotencyKey}' allaqachon ishlatilgan.")
            => IdempotencyKey = idempotencyKey;
    }
}
