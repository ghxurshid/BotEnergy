using Domain.Dtos.Base;

namespace Domain.Guards
{
    /// <summary>
    /// Bitta amal uchun to'sqinlik omillarini <b>ketma-ket va qisqa-tutashuv bilan</b> tekshiradigan
    /// zanjir. Birinchi topilgan to'siqdan keyin qolgan shartlar UMUMAN hisoblanmaydi —
    /// shu sababli null tekshiruvidan keyingi shartlarni <c>() =&gt; ...</c> ko'rinishida berish kerak.
    ///
    /// <code>
    /// var stop = await StopFactorCheck.For(StopActions.CashBoxOpen)
    ///     .StopIf(device is null,               StopFactors.Device.NotFound)
    ///     .StopIf(() =&gt; !device!.IsActive,      StopFactors.Device.Inactive)
    ///     .StopIf(() =&gt; !device!.IsReachable(), () =&gt; StopFactors.Device.Offline(device!.SerialNumber, device.LastSeenAt))
    ///     .StopIfAsync(() =&gt; _repo.HasOpenCollectionAsync(device!.Id), StopFactors.Device.HasOpenCollection)
    ///     .ResultAsync();
    ///
    /// if (stop is not null)
    ///     return GenericDto&lt;T&gt;.Blocked(stop);
    /// </code>
    ///
    /// Qoida: amal DB'ga birinchi yozuvni qilishdan OLDIN butun zanjir o'tishi shart.
    /// "Yozuvni yaratib, keyin buyruq yiqilsa Failed qilamiz" — bu qoidaning buzilishi.
    /// </summary>
    public sealed class StopFactorCheck
    {
        private StopFactorCheck(string action) => Action = action;

        /// <summary>Tekshirilayotgan amal nomi — log va diagnostika uchun.</summary>
        public string Action { get; }

        /// <summary>Topilgan birinchi to'siq; <c>null</c> bo'lsa amalni bajarish mumkin.</summary>
        public StopFactor? Blocker { get; private set; }

        public bool IsBlocked => Blocker is not null;

        public static StopFactorCheck For(string action) => new(action);

        // ── Sinxron shartlar ──────────────────────────────────────────

        /// <summary>Arzon va xavfsiz (null'ga bog'liq bo'lmagan) shart uchun.</summary>
        public StopFactorCheck StopIf(bool condition, StopFactor factor)
        {
            if (Blocker is null && condition)
                Blocker = factor;
            return this;
        }

        /// <summary>
        /// Kechiktirilgan shart — avvalgi tekshiruv yiqilgan bo'lsa umuman hisoblanmaydi.
        /// Null tekshiruvidan keyingi barcha shartlar shu shaklda yozilishi kerak.
        /// </summary>
        public StopFactorCheck StopIf(Func<bool> condition, StopFactor factor)
        {
            if (Blocker is null && condition())
                Blocker = factor;
            return this;
        }

        /// <summary>Xabari obyekt maydonlaridan quriladigan to'siq uchun (matn ham kechiktiriladi).</summary>
        public StopFactorCheck StopIf(Func<bool> condition, Func<StopFactor> factor)
        {
            if (Blocker is null && condition())
                Blocker = factor();
            return this;
        }

        // ── Asinxron shartlar (DB so'rovi) ────────────────────────────

        /// <summary>
        /// DB so'rovi talab qiladigan shart. Avvalgi to'siq topilgan bo'lsa so'rov YUBORILMAYDI —
        /// ortiqcha yuk bo'lmaydi.
        /// </summary>
        public async Task<StopFactorCheck> StopIfAsync(Func<Task<bool>> condition, StopFactor factor)
        {
            if (Blocker is null && await condition())
                Blocker = factor;
            return this;
        }

        public async Task<StopFactorCheck> StopIfAsync(Func<Task<bool>> condition, Func<StopFactor> factor)
        {
            if (Blocker is null && await condition())
                Blocker = factor();
            return this;
        }

        /// <summary>
        /// So'rov natijasini o'lchov sifatida ishlatadigan shart — masalan "nechta qurilma bog'langan".
        /// Natija xabarga ham kiritiladi, shuning uchun bitta so'rov bilan hal bo'ladi.
        /// </summary>
        public async Task<StopFactorCheck> StopIfCountAsync(Func<Task<int>> counter, Func<int, StopFactor> factor)
        {
            if (Blocker is null)
            {
                var count = await counter();
                if (count > 0)
                    Blocker = factor(count);
            }
            return this;
        }

        // ── Yakun ─────────────────────────────────────────────────────

        /// <summary>Topilgan to'siq yoki <c>null</c>.</summary>
        public StopFactor? Result() => Blocker;

        /// <summary>Zanjirni <c>GenericDto</c> xatosiga o'giradi (faqat to'siq bor bo'lsa chaqiriladi).</summary>
        public GenericDto<T> ToError<T>() => GenericDto<T>.Blocked(Blocker!);
    }

    /// <summary>
    /// <see cref="StopFactorCheck"/> zanjirini <c>await</c>siz davom ettirish uchun kengaytmalar —
    /// sinxron va asinxron shartlarni aralash yozish imkonini beradi.
    /// </summary>
    public static class StopFactorCheckExtensions
    {
        public static async Task<StopFactorCheck> StopIf(
            this Task<StopFactorCheck> chain, bool condition, StopFactor factor)
            => (await chain).StopIf(condition, factor);

        public static async Task<StopFactorCheck> StopIf(
            this Task<StopFactorCheck> chain, Func<bool> condition, StopFactor factor)
            => (await chain).StopIf(condition, factor);

        public static async Task<StopFactorCheck> StopIf(
            this Task<StopFactorCheck> chain, Func<bool> condition, Func<StopFactor> factor)
            => (await chain).StopIf(condition, factor);

        public static async Task<StopFactorCheck> StopIfAsync(
            this Task<StopFactorCheck> chain, Func<Task<bool>> condition, StopFactor factor)
            => await (await chain).StopIfAsync(condition, factor);

        public static async Task<StopFactorCheck> StopIfAsync(
            this Task<StopFactorCheck> chain, Func<Task<bool>> condition, Func<StopFactor> factor)
            => await (await chain).StopIfAsync(condition, factor);

        public static async Task<StopFactorCheck> StopIfCountAsync(
            this Task<StopFactorCheck> chain, Func<Task<int>> counter, Func<int, StopFactor> factor)
            => await (await chain).StopIfCountAsync(counter, factor);

        /// <summary>Zanjir yakuni — topilgan to'siq yoki <c>null</c>.</summary>
        public static async Task<StopFactor?> ResultAsync(this Task<StopFactorCheck> chain)
            => (await chain).Blocker;
    }
}
