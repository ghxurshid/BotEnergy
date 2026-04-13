using System.Text;
using System.Text.Json;
using RabbitMQ.Client;

namespace CommonConfiguration.Messaging
{
    /// <summary>
    /// RabbitMQ ga xabar yuborish uchun umumiy publisher.
    /// </summary>
    public sealed class RabbitMqPublisher
    {
        private readonly RabbitMqConnectionManager _connectionManager;
        private readonly JsonSerializerOptions _jsonOpts = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        public RabbitMqPublisher(RabbitMqConnectionManager connectionManager)
        {
            _connectionManager = connectionManager;
        }

        public void Publish<T>(string queueName, T message)
        {
            using var channel = _connectionManager.CreateChannel();
            channel.QueueDeclare(queueName, durable: true, exclusive: false, autoDelete: false);

            var json = JsonSerializer.Serialize(message, _jsonOpts);
            var body = Encoding.UTF8.GetBytes(json);

            var props = channel.CreateBasicProperties();
            props.Persistent = true;

            channel.BasicPublish(exchange: "", routingKey: queueName, basicProperties: props, body: body);
        }
    }
}
