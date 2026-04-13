using Application.BackgroundServices;
using Application.Services;
using CommonConfiguration.Messaging;
using CommonConfiguration.Redis;
using Domain.Enums;
using Domain.Interfaces;
using Domain.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Persistence.Context;
using Persistence.Repositories;
using StackExchange.Redis;

namespace CommonConfiguration.ConfigurationExtensions
{
    public static class ConfigurationAddExtensions
    {
        public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration config)
        {
            var connectionString = config.GetConnectionString("DefaultConnection");

            var dataSourceBuilder = new NpgsqlDataSourceBuilder(connectionString);
            dataSourceBuilder.MapEnum<UserType>("auth.user_type");
            var dataSource = dataSourceBuilder.Build();

            services.AddDbContext<AppDbContext>(options =>
                options.UseNpgsql(dataSource, npgsql =>
                    npgsql.MigrationsHistoryTable("__EFMigrationsHistory", "public"))
                .ConfigureWarnings(w =>
                    w.Ignore(RelationalEventId.PendingModelChangesWarning)));

            return services;
        }

        /// <summary>
        /// RabbitMQ ulanishi va publisher/consumer infrastrukturasini ro'yxatdan o'tkazish.
        /// </summary>
        public static IServiceCollection AddRabbitMq(this IServiceCollection services, IConfiguration config)
        {
            services.Configure<RabbitMqOptions>(config.GetSection("RabbitMq"));
            services.AddSingleton<RabbitMqConnectionManager>();
            services.AddSingleton<RabbitMqPublisher>();

            return services;
        }

        /// <summary>
        /// Redis ulanishi va device lock servisini ro'yxatdan o'tkazish.
        /// </summary>
        public static IServiceCollection AddRedisServices(this IServiceCollection services, IConfiguration config)
        {
            var redisConnectionString = config.GetSection("Redis:ConnectionString").Value ?? "localhost:6379";
            services.AddSingleton<IConnectionMultiplexer>(ConnectionMultiplexer.Connect(redisConnectionString));
            services.AddSingleton<IDeviceLockService, RedisDeviceLockService>();

            return services;
        }

        public static IServiceCollection RegisterServices(this IServiceCollection services)
        {
            // Auth
            services.AddScoped<IAuthService, AuthService>();
            services.AddScoped<ITokenService, TokenService>();
            services.AddScoped<IOtpService, OtpService>();

            // User
            services.AddScoped<IUserRepository, UserRepository>();
            services.AddScoped<IUserService, UserService>();
            services.AddScoped<IUserAdminService, UserAdminService>();

            // Role
            services.AddScoped<IRoleRepository, RoleRepository>();
            services.AddScoped<IRoleService, RoleService>();

            // Organization
            services.AddScoped<IOrganizationRepository, OrganizationRepository>();
            services.AddScoped<IOrganizationService, OrganizationService>();

            // Station
            services.AddScoped<IStationRepository, StationRepository>();
            services.AddScoped<IStationService, StationService>();

            // Device
            services.AddScoped<IDeviceRepository, DeviceRepository>();
            services.AddScoped<IDeviceService, DeviceService>();

            // Product
            services.AddScoped<IProductRepository, ProductRepository>();
            services.AddScoped<IProductService, ProductService>();

            // Client
            services.AddScoped<IClientRepository, ClientRepository>();
            services.AddScoped<IClientService, ClientService>();

            // Billing
            services.AddScoped<IBillingService, BillingService>();

            return services;
        }

        /// <summary>
        /// UserApi uchun sessiya bilan bog'liq servislar.
        /// SessionService, repository lar va idle session cleaner.
        /// </summary>
        public static IServiceCollection RegisterSessionServices(this IServiceCollection services)
        {
            services.AddScoped<IUserRepository, UserRepository>();
            services.AddScoped<IDeviceRepository, DeviceRepository>();
            services.AddScoped<IProductRepository, ProductRepository>();
            services.AddScoped<ISessionRepository, SessionRepository>();
            services.AddScoped<ISessionService, SessionService>();
            // ISessionNotifier — UserApi Program.cs da ro'yxatdan o'tkaziladi
            services.AddHostedService<IdleSessionCleanerService>();

            return services;
        }

        /// <summary>
        /// DeviceApi uchun faqat qurilma repositorysi.
        /// </summary>
        public static IServiceCollection RegisterDeviceServices(this IServiceCollection services)
        {
            services.AddScoped<IDeviceRepository, DeviceRepository>();

            return services;
        }
    }
}
