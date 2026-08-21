using Domain.Interfaces;
using Domain.Options;
using Domain.Repositories;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Application.BackgroundServices
{
    /// <summary>
    /// Naqd → karta oqimining fon xizmati. Ikki vazifa:
    ///
    ///  1. <c>PayoutFailed</c> sessiyalarni qayta urinish — bank transport xatosi vaqtinchalik
    ///     bo'lishi mumkin. Barcha holat DB'da (<c>NextAttemptAt</c>), shuning uchun servis
    ///     qayta ishga tushsa ham navbat yo'qolmaydi. Lease bir sessiyani ikki instansiya
    ///     olishiga yo'l qo'ymaydi.
    ///
    ///  2. Harakatsiz sessiyalarni yopish — mijoz pul solib ketib qolsa, pul uning kartasiga
    ///     avtomatik o'tkaziladi (bill acceptor pulni qaytara olmaydi).
    ///
    /// Natija qurilmaga <c>cash.session.result</c> bilan push qilinadi.
    /// </summary>
    public class CashPayoutWatcherService : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly CashTopUpOptions _options;
        private readonly ILogger<CashPayoutWatcherService> _logger;
        private readonly string _ownerId;

        private DateTime _nextIdleCheck = DateTime.MinValue;

        public CashPayoutWatcherService(
            IServiceScopeFactory scopeFactory,
            IOptions<CashTopUpOptions> options,
            ILogger<CashPayoutWatcherService> logger)
        {
            _scopeFactory = scopeFactory;
            _options = options.Value;
            _logger = logger;
            _ownerId = $"{Environment.MachineName}:{Guid.NewGuid():N}";
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var interval = TimeSpan.FromSeconds(Math.Max(1, _options.WatcherIntervalSeconds));
            _logger.LogInformation(
                "[CASH-WATCH] Watcher ishga tushdi owner={Owner} interval={Interval}s", _ownerId, interval.TotalSeconds);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(interval, stoppingToken);

                    await ProcessDuePayoutsAsync(stoppingToken);

                    // Idle tekshiruvi kamroq chastotada — u butun jadvalni skanerlaydi.
                    if (DateTime.Now >= _nextIdleCheck)
                    {
                        _nextIdleCheck = DateTime.Now.AddSeconds(Math.Max(10, _options.IdleCheckIntervalSeconds));
                        await CloseIdleSessionsAsync(stoppingToken);
                    }
                }
                catch (OperationCanceledException) { break; }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "[CASH-WATCH] Watcher tick xatosi.");
                }
            }
        }

        private async Task ProcessDuePayoutsAsync(CancellationToken ct)
        {
            using var scope = _scopeFactory.CreateScope();
            var sessionRepo = scope.ServiceProvider.GetRequiredService<ICashSessionRepository>();
            var cashService = scope.ServiceProvider.GetRequiredService<ICashTopUpService>();
            var publisher = scope.ServiceProvider.GetRequiredService<IDeviceCommandPublisher>();

            var leaseUntil = DateTime.Now.AddSeconds(Math.Max(5, _options.LeaseSeconds));
            var due = await sessionRepo.ClaimDueAsync(_ownerId, leaseUntil, Math.Max(1, _options.BatchSize));

            if (due.Count == 0)
                return;

            _logger.LogInformation("[CASH-WATCH] {Count} ta sessiya qayta urinish uchun olindi", due.Count);

            foreach (var session in due)
            {
                if (ct.IsCancellationRequested)
                    break;

                try
                {
                    var result = await cashService.RetryPayoutAsync(session.Id, ct);
                    if (!result.IsSuccess || result.Result is null)
                        continue;

                    // Qurilma commit paytida "kutilmoqda" javobini olgan edi — yakuniy natijani push qilamiz.
                    await publisher.PublishCashSessionResultAsync(session.SerialNumber, result.Result, ct);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "[CASH-WATCH] sessionId={SessionId} qayta urinish xatosi", session.Id);
                    await sessionRepo.ReleaseLeaseAsync(session.Id, _ownerId);
                }
            }
        }

        private async Task CloseIdleSessionsAsync(CancellationToken ct)
        {
            using var scope = _scopeFactory.CreateScope();
            var cashService = scope.ServiceProvider.GetRequiredService<ICashTopUpService>();

            var closed = await cashService.CloseIdleSessionsAsync(ct);
            if (closed > 0)
                _logger.LogInformation("[CASH-WATCH] {Count} ta harakatsiz sessiya yopildi", closed);
        }
    }
}
