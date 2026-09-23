namespace SessionApi.Models.Requests
{
    /// <summary>
    /// Qurilmadagi QR kod ichidagi ma'lumot: qurilmaning seriya raqami.
    /// </summary>
    public class ConnectByDeviceRequest
    {
        public string SerialNumber { get; set; } = string.Empty;
    }
}
