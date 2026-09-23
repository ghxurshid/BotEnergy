using Domain.Interfaces.Telegram;
using Domain.Options;
using Microsoft.Extensions.Options;

namespace AuthApi.BackgroundServices
{
    /// <summary>
    /// Telegram'dan xabarlarni long polling (<c>getUpdates</c>) bilan oladi.
    ///
    /// Nega webhook emas: serverda hozir ommaviy domen va TLS sertifikat yo'q,
    /// Telegram esa webhook uchun HTTPS talab qiladi. Polling hech qanday tashqi
    /// sozlamasiz ishlaydi; domen paydo bo'lsa webhook'ga o'tish mumkin.
    ///
    /// Nega AuthApi: kodlar shu jarayonda yaratiladi va AuthApi bitta systemd
    /// nusxasida ishlaydi (ko'p nusxa bo'lsa xabarlar ikki marta o'qilardi).
    /// </summary>
    public sealed class TelegramUpdatePollerService : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ITelegramBotClient _bot;
        private readonly TelegramOptions _options;
        private readonly ILogger<TelegramUpdatePollerService> _logger;

        private long _offset;

        public TelegramUpdatePollerService(
            IServiceScopeFactory scopeFactory,
            ITelegramBotClient bot,
            IOptions<TelegramOptions> options,
            ILogger<TelegramUpdatePollerService> logger)
        {
            _scopeFactory = scopeFactory;
            _bot = bot;
            _options = options.Value;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            if (!_bot.IsConfigured)
            {
                _logger.LogInformation("[TG] Bot tokeni berilmagan — tinglash yoqilmadi.");
                return;
            }

            _logger.LogInformation("[TG] Xabarlarni tinglash boshlandi.");

            var pollTimeout = Math.Clamp(_options.TimeoutSeconds, 10, 60);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    var updates = await _bot.GetUpdatesAsync(_offset, pollTimeout, stoppingToken);

                    foreach (var update in updates)
                    {
                        _offset = update.UpdateId + 1;

                        // Har xabar o'z scope'ida — repozitoriylar scoped.
                        using var scope = _scopeFactory.CreateScope();
                        var gateway = scope.ServiceProvider.GetRequiredService<ITelegramGateway>();

                        try
                        {
                            await gateway.HandleUpdateAsync(update, stoppingToken);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "[TG] Xabarni qayta ishlashda xatolik updateId={Id}", update.UpdateId);
                        }
                    }
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "[TG] Tinglashda xatolik — 5 soniyadan keyin qayta urinamiz.");
                    await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
                }
            }

            _logger.LogInformation("[TG] Tinglash to'xtadi.");
        }
    }
}
