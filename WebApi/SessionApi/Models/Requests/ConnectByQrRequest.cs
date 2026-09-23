namespace SessionApi.Models.Requests
{
    /// <summary>
    /// Kolonka ekranidagi QR kod. <c>BE1:</c> prefiksi bilan ham, usiz ham qabul qilinadi.
    /// </summary>
    public class ConnectByQrRequest
    {
        public string Code { get; set; } = string.Empty;
    }
}
