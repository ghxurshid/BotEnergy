using Domain.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Application.BackgroundServices
{
    /// <summary>
    /// Davriy tozalash:
    ///   - 30 daqiqadan beri faol bo'lmagan sessiyalarni yopadi (idle timeout).
    ///   - Offline qurilmalarning aktiv sessiyalarini yopadi (90s heartbeat threshold).
    ///   - Stop/pause tasdig'i kelmagan "stalled" jarayonlarni majburan yakunlaydi (60s watchdog).
    /// 30 soniyada bir ishlaydi — watchdog va offline aniqlash o'z vaqtida bo'lishi uchun.
    /// So'rovlar indeksli va arzon.
    /// </summary>
    public class IdleSessionCleanerService : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<IdleSessionCleanerService> _logger;
        private static readonly TimeSpan CheckInterval = TimeSpan.FromSeconds(30);

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
                    var processService = scope.ServiceProvider.GetRequiredService<IProcessService>();
                    await sessionService.CloseTimedOutSessionsAsync();
                    await sessionService.PauseOfflineDeviceSessionsAsync();
                    await processService.FinalizeStalledProcessesAsync();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Idle session cleanup xatosi.");
                }
            }
        }
    }
}
