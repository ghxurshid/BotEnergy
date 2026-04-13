using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace CommonConfiguration.Messaging
{
    /// <summary>
    /// RabbitMQ dan xabarlarni qabul qilish uchun bazaviy BackgroundService.
    /// Har bir consumer bu klassdan meros olib, HandleMessageAsync ni implementatsiya qiladi.
    /// </summary>
    public abstract class RabbitMqConsumerBase<T> : BackgroundService
    {
        private readonly RabbitMqConnectionManager _connectionManager;
        private readonly ILogger _logger;
        private readonly string _queueName;
        private IModel? _channel;

        private static readonly JsonSerializerOptions JsonOpts = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true
        };

        protected RabbitMqConsumerBase(
            RabbitMqConnectionManager connectionManager,
            ILogger logger,
            string queueName)
        {
            _connectionManager = connectionManager;
            _logger = logger;
            _queueName = queueName;
        }

        protected abstract Task HandleMessageAsync(T message);

        protected override Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _channel = _connectionManager.CreateChannel();
            _channel.QueueDeclare(_queueName, durable: true, exclusive: false, autoDelete: false);
            _channel.BasicQos(0, 10, false);

            var consumer = new AsyncEventingBasicConsumer(_channel);
            consumer.Received += async (_, ea) =>
            {
                try
                {
                    var json = Encoding.UTF8.GetString(ea.Body.ToArray());
                    var message = JsonSerializer.Deserialize<T>(json, JsonOpts);

                    if (message is not null)
                        await HandleMessageAsync(message);

                    _channel.BasicAck(ea.DeliveryTag, false);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "RabbitMQ xabar qayta ishlashda xato. Queue: {Queue}", _queueName);
                    _channel.BasicNack(ea.DeliveryTag, false, true);
                }
            };

            _channel.BasicConsume(_queueName, autoAck: false, consumer);
            _logger.LogInformation("RabbitMQ consumer boshlandi: {Queue}", _queueName);

            return Task.CompletedTask;
        }

        public override void Dispose()
        {
            _channel?.Dispose();
            base.Dispose();
        }
    }
}
