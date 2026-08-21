namespace Domain.Dtos.Cash
{
    /// <summary>
    /// Kupyura qo'shish natijasi.
    ///
    /// <paramref name="Added"/> = <c>false</c> — shu <c>BillSeq</c> allaqachon yozilgan
    /// (qurilma xabarni qayta yuborgan). Bu xato emas: jami summa o'zgarmaydi va
    /// qurilmaga o'sha paytdagi haqiqiy summa qaytariladi.
    /// </summary>
    public sealed record CashBillAddResult(bool Added, decimal AcceptedTotal, int BillCount);
}
