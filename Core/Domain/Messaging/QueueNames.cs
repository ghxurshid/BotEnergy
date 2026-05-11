namespace Domain.Messaging
{
    public static class QueueNames
    {
        public const string CommandQueue = "device.commands";
        public const string EventQueue = "device.events";

        // Payment oqimi alohida queue'larda — telemetry/jarayon trafiki bilan aralashmasin
        // (pul-tegishli kod uchun mustaqil backpressure va monitoring).
        public const string PaymentEventQueue = "device.payment.events";
        public const string PaymentCommandQueue = "device.payment.commands";
    }
}
