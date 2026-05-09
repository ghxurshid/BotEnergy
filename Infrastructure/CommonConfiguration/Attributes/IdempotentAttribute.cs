namespace CommonConfiguration.Attributes
{
    /// <summary>
    /// Action ni "Idempotency-Key" header bo'yicha cache qilish uchun marker.
    /// Required = true bo'lsa, header yo'q bo'lganda 400 qaytadi.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, Inherited = false, AllowMultiple = false)]
    public sealed class IdempotentAttribute : Attribute
    {
        public bool Required { get; set; } = false;
    }
}
