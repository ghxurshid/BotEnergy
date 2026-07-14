namespace Domain.Helpers
{
    /// <summary>
    /// Decimal UZS (mavjud stack) ↔ integer tiyin (hold invoice jadvallari, Payme API)
    /// o'rtasidagi YAGONA konversiya nuqtasi. 1 so'm = 100 tiyin.
    /// </summary>
    public static class Money
    {
        public static long ToTiyin(decimal uzs)
            => (long)Math.Round(uzs * 100m, MidpointRounding.AwayFromZero);

        public static decimal ToUzs(long tiyin)
            => tiyin / 100m;
    }
}
