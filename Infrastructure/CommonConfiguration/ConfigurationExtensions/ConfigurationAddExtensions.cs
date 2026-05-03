using Application.BackgroundServices;
using Application.Services;
using CommonConfiguration.Messaging;
using CommonConfiguration.Redis;
using Domain.Enums;
using Domain.Interfaces;
using Domain.Repositories;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Npgsql;
using Persistence.Context;
using Persistence.Repositories;
using StackExchange.Redis;
using Swashbuckle.AspNetCore.SwaggerGen;
using System.Text;

namespace CommonConfiguration.ConfigurationExtensions
{
    public static class ConfigurationAddExtensions
    {
        /// <summary>
        /// DI konfiguratsiyasini barcha API lar uchun bir xil qiladi.
        /// Build vaqtida service graph validatsiya qilinadi.
        /// </summary>
        public static WebApplicationBuilder AddValidatedServiceProvider(this WebApplicationBuilder builder)
        {
            builder.Host.UseDefaultServiceProvider((context, options) =>
            {
                options.ValidateOnBuild = true;
                options.ValidateScopes = context.HostingEnvironment.IsDevelopment();
            });

            return builder;
        }

        /// <summary>
        /// Swagger + JWT Bearer auth konfiguratsiyasi.
        /// <paramref name="includeJwtAuth"/> false bo'lsa, faqat Swagger doc va XML comments qo'shiladi (masalan, AuthApi).
        /// </summary>
        public static IServiceCollection AddSwaggerWithJwtAuth(
            this IServiceCollection services,
            string title,
            string version,
            string description,
            bool includeJwtAuth = true)
        {
            services.AddEndpointsApiExplorer();
            services.AddSwaggerGen(options =>
            {
                options.SwaggerDoc(version, new OpenApiInfo
                {
                    Title = title,
                    Version = version,
                    Description = description
                });

                var xmlFile = $"{System.Reflection.Assembly.GetEntryAssembly()!.GetName().Name}.xml";
                var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
                if (File.Exists(xmlPath))
                    options.IncludeXmlComments(xmlPath);

                if (includeJwtAuth)
                {
                    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
                    {
                        Name = "Authorization",
                        Type = SecuritySchemeType.Http,
                        Scheme = "Bearer",
                        BearerFormat = "JWT",
                        In = ParameterLocation.Header,
                        Description = "JWT tokenni kiriting"
                    });
                    options.AddSecurityRequirement(new OpenApiSecurityRequirement
                    {
                        {
                            new OpenApiSecurityScheme
                            {
                                Reference = new OpenApiReference
                                {
                                    Type = ReferenceType.SecurityScheme,
                                    Id = "Bearer"
                                }
                            },
                            Array.Empty<string>()
                        }
                    });
                }
            });

            return services;
        }

        private const string DefaultJwtSecret = "3f1e2d4c5a6b7c8d9e0f1a2b3c4d5e6f7a8b9c0d1e2f3a4b5c6d7e8f9a0b1c2d";

        /// <summary>
        /// JWT Bearer autentifikatsiya. SignalR ishlatadigan API lar uchun
        /// <paramref name="signalRHubPath"/> ni ko'rsating (masalan, "/hubs").
        /// </summary>
        public static IServiceCollection AddJwtAuthentication(
            this IServiceCollection services,
            IConfiguration config,
            string? signalRHubPath = null)
        {
            services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
                .AddJwtBearer(options =>
                {
                    options.TokenValidationParameters = new TokenValidationParameters
                    {
                        ValidateIssuer = false,
                        ValidateAudience = false,
                        ValidateLifetime = true,
                        ValidateIssuerSigningKey = true,
                        IssuerSigningKey = new SymmetricSecurityKey(
                            Encoding.UTF8.GetBytes(
                                config["Jwt:Secret"] ?? DefaultJwtSecret))
                    };

                    if (signalRHubPath is not null)
                    {
                        options.Events = new JwtBearerEvents
                        {
                            OnMessageReceived = context =>
                            {
                                var accessToken = context.Request.Query["access_token"];
                                var path = context.HttpContext.Request.Path;
                                if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments(signalRHubPath))
                                    context.Token = accessToken;
                                return Task.CompletedTask;
                            }
                        };
                    }
                });

            return services;
        }

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
            var redisOptions = ConfigurationOptions.Parse(redisConnectionString);
            redisOptions.AbortOnConnectFail = false; // Redis yo'q bo'lsa ham app crash qilmaydi

            services.AddSingleton<IConnectionMultiplexer>(ConnectionMultiplexer.Connect(redisOptions));
            services.AddSingleton<IDeviceLockService, RedisDeviceLockService>();

            services.AddSingleton<RedisRefreshTokenStore>();
            services.AddSingleton<InMemoryRefreshTokenStore>();
            services.AddSingleton<IRefreshTokenStore, ResilientRefreshTokenStore>();

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

            // Merchant
            services.AddScoped<IMerchantRepository, MerchantRepository>();
            services.AddScoped<IMerchantService, MerchantService>();

            // Billing
            services.AddScoped<IProductProcessRepository, ProductProcessRepository>();
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
            services.AddScoped<IProductProcessRepository, ProductProcessRepository>();
            services.AddScoped<ISessionRepository, SessionRepository>();
            services.AddScoped<IProcessService, ProcessService>();
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
