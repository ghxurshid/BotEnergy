using Domain.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Application.BackgroundServices
{
    /// <summary>
    /// Har 5 daqiqada ishlaydi — 30 daqiqadan beri faol bo'lmagan sessiyalarni yopadi.
    /// Aktiv jarayonlar mavjud bo'lsa, ularga avval MQTT stop yuboriladi va balansdan yechiladi.
    /// </summary>
    public class IdleSessionCleanerService : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<IdleSessionCleanerService> _logger;
        private static readonly TimeSpan CheckInterval = TimeSpan.FromMinutes(5);

        public IdleSessionCleanerService(
            IServiceScopeFactory scopeFactory,
            ILogger<IdleSessionCleanerService> logger)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                await Task.Delay(CheckInterval, stoppingToken);

                try
                {
                    using var scope = _scopeFactory.CreateScope();
                    var sessionService = scope.ServiceProvider.GetRequiredService<ISessionService>();
                    await sessionService.CloseTimedOutSessionsAsync();
                    await sessionService.CloseOfflineDeviceSessionsAsync();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Idle session cleanup xatosi.");
                }
            }
        }
    }
}
