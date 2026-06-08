namespace Domain.Enums
{
    /// <summary>
    /// Qurilmaning ulanish holati o'zgarishi (real-time event uchun).
    /// </summary>
    public enum DeviceConnectivity
    {
        /// <summary>Qurilma aloqaga chiqdi / heartbeat qayta boshlandi (offline → online edge).</summary>
        Online,

        /// <summary>Qurilma aloqadan uzildi (aktiv sessiyasiz).</summary>
        Offline,

        /// <summary>Qurilma aktiv sessiya davomida uzildi (sessiya yopildi).</summary>
        Lost
    }
}
