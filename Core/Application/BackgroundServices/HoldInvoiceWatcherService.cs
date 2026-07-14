using Domain.Interfaces;
using Domain.Options;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Application.BackgroundServices
{
    /// <summary>
    /// Hold invoice watcher — Payme webhook yubormagani uchun barcha holat sinxronizatsiyasi
    /// polling orqali:
    ///  - WaitingForConfirmation → receipts.check (Hold bo'lsa balans + notify; TTL → Expired);
    ///  - CapturePending → confirm_hold (retry/backoff);
    ///  - RefundPending → cancel (retry/backoff);
    ///  - Settling sessiyalarni yopish.
    /// Restart-safe: barcha holat DB'da, NextAttemptAt qayta yuritadi. Lease bilan bir invoice
    /// bir vaqtda faqat bitta instance/tick tomonidan olinadi.
    /// </summary>
    public class HoldInvoiceWatcherService : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly HoldInvoiceOptions _options;
        private readonly ILogger<HoldInvoiceWatcherService> _logger;
        private readonly string _ownerId;

        public HoldInvoiceWatcherService(
            IServiceScopeFactory scopeFactory,
            IOptions<HoldInvoiceOptions> options,
            ILogger<HoldInvoiceWatcherService> logger)
        {
            _scopeFactory = scopeFactory;
            _options = options.Value;
            _logger = logger;
            _ownerId = $"{Environment.MachineName}:{Guid.NewGuid():N}";
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var interval = TimeSpan.FromSeconds(Math.Max(1, _options.WatcherIntervalSeconds));
            _logger.LogInformation("[HOLD-WATCH] Watcher ishga tushdi owner={Owner} interval={Interval}s", _ownerId, interval.TotalSeconds);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(interval, stoppingToken);

                    using var scope = _scopeFactory.CreateScope();
                    var settlement = scope.ServiceProvider.GetRequiredService<IHoldSettlementService>();

                    await settlement.ProcessDueInvoicesAsync(_ownerId, stoppingToken);
                    await settlement.FinalizeSettledSessionsAsync(stoppingToken);
                }
                catch (OperationCanceledException) { break; }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "[HOLD-WATCH] Watcher tick xatosi.");
                }
            }
        }
    }
}
