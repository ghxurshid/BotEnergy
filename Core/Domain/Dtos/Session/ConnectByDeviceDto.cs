namespace Domain.Dtos.Session
{
    /// <summary>
    /// Mijoz qurilmadagi (kolonkadagi) QR kodni skanerlab sessiya ochishi.
    ///
    /// Bu — <see cref="CreateSessionDto"/> ning teskari yo'nalishi: u yerda telefon QR
    /// ko'rsatadi va qurilma reader uni o'qiydi, bu yerda esa telefon qurilmaning
    /// seriya raqamli QR stikerini o'qiydi. Ikkala yo'l ham bir xil natijaga olib
    /// keladi — DB'da Connected statusli sessiya va to'lov konteksti.
    /// </summary>
    public class ConnectByDeviceDto
    {
        public long UserId { get; set; }

        /// <summary>QR ichidagi qurilma seriya raqami.</summary>
        public string SerialNumber { get; set; } = string.Empty;
    }
}
