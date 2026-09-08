using Domain.Options;
using Domain.Payments;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Application.BackgroundServices
{
    /// <summary>
    /// To'lov watcher'i — provider webhook yubormaydigan usullar uchun holat sinxronizatsiyasi
    /// polling orqali, callback yuboradigan usullar uchun esa faqat retry/settlement navbati:
    ///  - WaitingForConfirmation → provider holatini so'rash (Funded bo'lsa balans + notify; TTL → Expired);
    ///  - SettlePending → capture (retry/backoff);
    ///  - RefundPending → refund/cancel (retry/backoff);
    ///  - Settling sessiyalarni yopish.
    ///
    /// Ro'yxatga olingan HAR BIR strategiya bo'ylab aylanadi — har biri faqat o'z usulidagi
    /// intent'larni claim qiladi (<c>ClaimDueAsync(method, ...)</c>).
    /// Restart-safe: barcha holat DB'da, NextAttemptAt qayta yuritadi. Lease bilan bir intent
    /// bir vaqtda faqat bitta instance/tick tomonidan olinadi.
    /// </summary>
    public class PaymentIntentWatcherService : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly PaymentOptions _options;
        private readonly ILogger<PaymentIntentWatcherService> _logger;
        private readonly string _ownerId;

        public PaymentIntentWatcherService(
            IServiceScopeFactory scopeFactory,
            IOptions<PaymentOptions> options,
            ILogger<PaymentIntentWatcherService> logger)
        {
            _scopeFactory = scopeFactory;
            _options = options.Value;
            _logger = logger;
            _ownerId = $"{Environment.MachineName}:{Guid.NewGuid():N}";
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var interval = TimeSpan.FromSeconds(Math.Max(1, _options.WatcherIntervalSeconds));
            _logger.LogInformation("[PAY-WATCH] Watcher ishga tushdi owner={Owner} interval={Interval}s",
                _ownerId, interval.TotalSeconds);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(interval, stoppingToken);

                    using var scope = _scopeFactory.CreateScope();
                    var resolver = scope.ServiceProvider.GetRequiredService<ISessionPaymentStrategyResolver>();

                    foreach (var strategy in resolver.All)
                    {
                        if (stoppingToken.IsCancellationRequested) break;
                        try
                        {
                            await strategy.ProcessDueAsync(_ownerId, stoppingToken);
                            await strategy.FinalizeSettledAsync(stoppingToken);
                        }
                        catch (OperationCanceledException) { throw; }
                        catch (Exception ex)
                        {
                            // Bitta strategiyaning xatosi qolganlarini to'xtatmasin.
                            _logger.LogError(ex, "[PAY-WATCH] {Method} strategiyasi tick'ida xato",
                                strategy.Profile.Method);
                        }
                    }
                }
                catch (OperationCanceledException) { break; }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "[PAY-WATCH] Watcher tick xatosi.");
                }
            }
        }
    }
}
