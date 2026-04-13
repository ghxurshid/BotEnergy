using Microsoft.Extensions.Options;
using RabbitMQ.Client;

namespace CommonConfiguration.Messaging
{
    /// <summary>
    /// RabbitMQ ulanishini boshqaradi. Singleton sifatida ro'yxatdan o'tkaziladi.
    /// </summary>
    public sealed class RabbitMqConnectionManager : IDisposable
    {
        private readonly ConnectionFactory _factory;
        private IConnection? _connection;
        private readonly object _lock = new();

        public RabbitMqConnectionManager(IOptions<RabbitMqOptions> options)
        {
            var opt = options.Value;
            _factory = new ConnectionFactory
            {
                HostName = opt.HostName,
                Port = opt.Port,
                UserName = opt.UserName,
                Password = opt.Password,
                VirtualHost = opt.VirtualHost,
                DispatchConsumersAsync = true
            };
        }

        public IConnection GetConnection()
        {
            if (_connection is { IsOpen: true })
                return _connection;

            lock (_lock)
            {
                if (_connection is { IsOpen: true })
                    return _connection;

                _connection = _factory.CreateConnection();
                return _connection;
            }
        }

        public IModel CreateChannel()
        {
            return GetConnection().CreateModel();
        }

        public void Dispose()
        {
            _connection?.Dispose();
        }
    }
}
