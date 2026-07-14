namespace SessionApi.Mqtt.Handlers
{
    /// <summary>
    /// Yagona ro'yxat — envelope.type qiymatlari handler va simulator/firmware orasida sinxron.
    /// Yangi handler qo'shganda shu yerga const qo'shing, attribute'da shu konstantani ishlating.
    /// </summary>
    public static class MqttHandlerTypes
    {
        // device → server
        public const string SessionConnect = "session.connect";
        public const string ProcessTelemetry = "process.telemetry";
        public const string ProcessFinished = "process.finished";
        public const string ProcessPaused = "process.paused";
        public const string DeviceHeartbeat = "device.heartbeat";
        public const string DeviceStatus = "device.status";
        public const string PaymentQr = "payment.qr";

        // server → device (publisher tomonidan ishlatiladi, handler emas)
        public const string ProcessStart = "process.start";
        public const string ProcessPause = "process.pause";
        public const string ProcessResume = "process.resume";
        public const string ProcessStop = "process.stop";
        public const string PaymentResult = "payment.result";
        public const string SessionClose = "session.close";
        public const string BalanceUpdate = "balance.update";
    }
}
