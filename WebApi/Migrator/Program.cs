// "CommonConfiguration" ham namespace, ham class nomi — alias bilan noaniqlik yo'qotiladi.
using CommonConfig = CommonConfiguration.ConfigurationServices.CommonConfiguration;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Npgsql;
using Persistence.Context;
using Persistence.Seed;

// ─────────────────────────────────────────────────────────────────────────────
// BotEnergy Migrator — migratsiya va seed'ni BIR MARTA, servislardan OLDIN qo'llaydi.
//
// Nega alohida jarayon:
//   • Ilgari 7 API bootda parallel MigrateAsync() chaqirardi. pg_advisory_lock poygani
//     bartaraf qilsa ham, 6 ta jarayon bekorga kutib turardi va noto'g'ri migratsiya
//     jimgina prodga chiqib ketardi — deploy loglarida u alohida qadam sifatida ko'rinmasdi.
//   • Endi deploy pipeline'ida migratsiya alohida qadam: u yiqilsa servislar UMUMAN
//     yangilanmaydi.
//
// Rejimlar:
//   (argumentsiz)   — migratsiya + seed
//   --migrate-only  — faqat migratsiya (seed'siz)
//   --seed-only     — faqat seed
//   --list          — qo'llanilgan va kutilayotgan migratsiyalarni ko'rsatadi, hech narsa o'zgartirmaydi
//
// Exit kodlari: 0 — muvaffaqiyat, 1 — xato (CI qadamni fail qiladi).
// ─────────────────────────────────────────────────────────────────────────────

var mode = args.FirstOrDefault()?.ToLowerInvariant();

using var loggerFactory = LoggerFactory.Create(builder => builder
    .AddSimpleConsole(options =>
    {
        options.SingleLine = true;
        options.TimestampFormat = "HH:mm:ss ";
    })
    .SetMinimumLevel(LogLevel.Information));

var logger = loggerFactory.CreateLogger("Migrator");

try
{
    var config = CommonConfig.GetConfiguration();
    var connectionString = config.GetConnectionString("DefaultConnection");

    if (string.IsNullOrWhiteSpace(connectionString) ||
        connectionString.StartsWith("Env_", StringComparison.Ordinal))
    {
        logger.LogError(
            "ConnectionStrings:DefaultConnection sozlanmagan. " +
            "Env var bering: ConnectionStrings__DefaultConnection");
        return 1;
    }

    var dataSourceBuilder = new NpgsqlDataSourceBuilder(connectionString);
    dataSourceBuilder.UseNetTopologySuite();   // PostGIS — StationEntity.Coordinates uchun
    await using var dataSource = dataSourceBuilder.Build();

    var options = new DbContextOptionsBuilder<AppDbContext>()
        .UseNpgsql(dataSource, npgsql => npgsql
            .MigrationsHistoryTable("__EFMigrationsHistory", "public")
            .UseNetTopologySuite())
        .UseLoggerFactory(loggerFactory)
        .Options;

    await using var context = new AppDbContext(options);

    if (mode == "--list")
    {
        var applied = (await context.Database.GetAppliedMigrationsAsync()).ToList();
        var pending = (await context.Database.GetPendingMigrationsAsync()).ToList();

        logger.LogInformation("Qo'llanilgan migratsiyalar: {Count}", applied.Count);
        foreach (var m in applied) logger.LogInformation("  [x] {Migration}", m);

        logger.LogInformation("Kutilayotgan migratsiyalar: {Count}", pending.Count);
        foreach (var m in pending) logger.LogInformation("  [ ] {Migration}", m);

        return 0;
    }

    if (mode != "--seed-only")
    {
        var pending = (await context.Database.GetPendingMigrationsAsync()).ToList();
        if (pending.Count == 0)
        {
            logger.LogInformation("Kutilayotgan migratsiya yo'q — baza allaqachon dolzarb.");
        }
        else
        {
            logger.LogInformation("Qo'llanadigan migratsiyalar: {Migrations}", string.Join(", ", pending));
            await context.Database.MigrateAsync();
            logger.LogInformation("Migratsiyalar muvaffaqiyatli qo'llandi.");
        }
    }

    if (mode != "--migrate-only")
    {
        // Seed:AdminPassword "Env_*" placeholder bo'lsa (env var berilmagan) parol sifatida
        // o'tib ketmasligi kerak — bu holda default admin umuman yaratilmaydi.
        var seedAdminPassword = config["Seed:AdminPassword"];
        if (string.IsNullOrWhiteSpace(seedAdminPassword) ||
            seedAdminPassword.StartsWith("Env_", StringComparison.Ordinal))
        {
            seedAdminPassword = null;
            logger.LogWarning(
                "Seed:AdminPassword berilmagan — default admin yaratilmaydi. " +
                "Kerak bo'lsa env var bering: Seed__AdminPassword.");
        }

        logger.LogInformation("Seed boshlandi...");
        await DataSeeder.SeedAsync(context, adminPassword: seedAdminPassword, isDevelopment: false);
        logger.LogInformation("Seed yakunlandi.");
    }

    return 0;
}
catch (Exception ex)
{
    logger.LogError(ex, "Migrator xato bilan yakunlandi.");
    return 1;
}
