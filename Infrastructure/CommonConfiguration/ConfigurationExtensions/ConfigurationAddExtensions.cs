using Application.BackgroundServices;
using Application.Services;
using CommonConfiguration.Payments.Payme;
using CommonConfiguration.Redis;
using CommonConfiguration.Reporting;
using Domain.Enums;
using Domain.Interfaces;
using Domain.Interfaces.Payme;
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

        // Faqat Development fallback. Production'da Jwt:Secret (env: Jwt__Secret) majburiy —
        // GetJwtSecret set qilinmagan bo'lsa prod'da exception otadi.
        private const string DevFallbackJwtSecret = "3f1e2d4c5a6b7c8d9e0f1a2b3c4d5e6f7a8b9c0d1e2f3a4b5c6d7e8f9a0b1c2d";

        /// <summary>
        /// Config'dagi secret qiymatni o'qiydi. Bo'sh yoki "Env_" bilan boshlangan qiymat
        /// (Configuration.Production.json'dagi placeholder — env var hali berilmagan) null sanaladi.
        /// </summary>
        internal static string? ResolveSecret(IConfiguration config, string key)
        {
            var value = config[key];
            if (string.IsNullOrWhiteSpace(value))
                return null;
            if (value.StartsWith("Env_", StringComparison.Ordinal))
                return null;
            return value;
        }

        /// <summary>
        /// JWT secret'ning yagona manbasi — imzolash (TokenService) ham, tekshirish
        /// (AddJwtAuthentication) ham shu metod orqali oladi.
        /// </summary>
        private static string GetJwtSecret(IConfiguration config)
        {
            var secret = ResolveSecret(config, "Jwt:Secret");
            if (secret is not null)
                return secret;

            var env = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Development";
            if (string.Equals(env, "Production", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException(
                    "Jwt:Secret production'da majburiy. Env var sifatida bering: Jwt__Secret=<kamida 64 belgi random>.");

            return DevFallbackJwtSecret;
        }

        /// <summary>
        /// JWT Bearer autentifikatsiya. SignalR ishlatadigan API lar uchun
        /// <paramref name="signalRHubPath"/> ni ko'rsating (masalan, "/hubs").
        /// <paramref name="acceptedAudiences"/> — shu API qabul qiladigan token guruhlari
        /// (<see cref="Domain.Auth.JwtAudiences"/>). Berilmasa ikkala guruh ham qabul qilinadi.
        /// </summary>
        public static IServiceCollection AddJwtAuthentication(
            this IServiceCollection services,
            IConfiguration config,
            string? signalRHubPath = null,
            params string[] acceptedAudiences)
        {
            var audiences = acceptedAudiences is { Length: > 0 }
                ? acceptedAudiences
                : Domain.Auth.JwtAudiences.All;

            services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
                .AddJwtBearer(options =>
                {
                    options.TokenValidationParameters = new TokenValidationParameters
                    {
                        ValidateIssuer = false,
                        ValidateAudience = true,
                        ValidAudiences = audiences,
                        ValidateLifetime = true,
                        ValidateIssuerSigningKey = true,
                        IssuerSigningKey = new SymmetricSecurityKey(
                            Encoding.UTF8.GetBytes(GetJwtSecret(config)))
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

        /// <summary>
        /// IP bo'yicha fixed-window rate limiting (default: 30 req/min, RateLimit:PermitPerMinute
        /// bilan sozlanadi). Limit oshsa 429 + Retry-After: 60. Brute-force'ga ochiq API'larda
        /// (birinchi navbatda AuthApi — login/OTP) yoqiladi; app.UseRateLimiter() ham kerak.
        /// </summary>
        public static IServiceCollection AddIpRateLimiting(this IServiceCollection services, IConfiguration config)
        {
            var permitPerMinute = int.TryParse(config["RateLimit:PermitPerMinute"], out var p) ? p : 30;

            services.AddRateLimiter(options =>
            {
                options.RejectionStatusCode = Microsoft.AspNetCore.Http.StatusCodes.Status429TooManyRequests;
                options.OnRejected = (context, _) =>
                {
                    context.HttpContext.Response.Headers.RetryAfter = "60";
                    return ValueTask.CompletedTask;
                };
                options.GlobalLimiter = System.Threading.RateLimiting.PartitionedRateLimiter
                    .Create<Microsoft.AspNetCore.Http.HttpContext, string>(httpContext =>
                        System.Threading.RateLimiting.RateLimitPartition.GetFixedWindowLimiter(
                            httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                            _ => new System.Threading.RateLimiting.FixedWindowRateLimiterOptions
                            {
                                PermitLimit = permitPerMinute,
                                Window = TimeSpan.FromMinutes(1),
                                QueueLimit = 0
                            }));
            });

            return services;
        }

        public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration config)
        {
            var connectionString = config.GetConnectionString("DefaultConnection");

            var dataSourceBuilder = new NpgsqlDataSourceBuilder(connectionString);
            dataSourceBuilder.UseNetTopologySuite(); // PostGIS geografik turlar (StationEntity.Coordinates) uchun
            var dataSource = dataSourceBuilder.Build();

            services.AddDbContext<AppDbContext>(options =>
                options.UseNpgsql(dataSource, npgsql =>
                    npgsql.MigrationsHistoryTable("__EFMigrationsHistory", "public")
                        .UseNetTopologySuite())
                .ConfigureWarnings(w =>
                    w.Ignore(RelationalEventId.PendingModelChangesWarning)));

            // /health endpoint uchun (Program.cs'da app.MapHealthChecks("/health") kerak).
            services.AddHealthChecks()
                .AddCheck<HealthChecks.DbHealthCheck>("database")
                .AddCheck<HealthChecks.RedisHealthCheck>("redis");

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

            services.AddSingleton<RedisPendingSessionStore>();
            services.AddSingleton<InMemoryPendingSessionStore>();
            services.AddSingleton<IPendingSessionStore, ResilientPendingSessionStore>();

            // Replay protection counter'lari Redis'da (TTL'siz) — restart'da yo'qolmaydi.
            // In-memory nusxa faqat Redis yiqilganda fallback/shadow sifatida ishlaydi.
            services.AddSingleton<RedisMqttMessageIdStore>();
            services.AddSingleton<InMemoryMqttMessageIdStore>();
            services.AddSingleton<IMqttMessageIdStore, ResilientMqttMessageIdStore>();

            services.AddSingleton<IIdempotencyStore, RedisIdempotencyStore>();
            services.AddScoped<Filters.IdempotencyFilter>();

            return services;
        }

        public static IServiceCollection RegisterServices(this IServiceCollection services)
        {
            // Bir nechta repo chaqiruvini bitta DB tranzaksiyada bajarish uchun
            services.AddScoped<ITransactionRunner, TransactionRunner>();

            // User repos (Platform / Customer alohida)
            services.AddScoped<IPlatformUserRepository, PlatformUserRepository>();
            services.AddScoped<ICustomerUserRepository, CustomerUserRepository>();
            services.AddScoped<IUserService, UserService>();
            services.AddScoped<IUserAdminService, UserAdminService>();
            services.AddScoped<ICustomerAdminService, CustomerAdminService>();

            // Role repos + permission katalog
            services.AddScoped<IPlatformRoleRepository, PlatformRoleRepository>();
            services.AddScoped<ICustomerRoleRepository, CustomerRoleRepository>();
            services.AddScoped<IPermissionRepository, PermissionRepository>();
            services.AddScoped<IRoleService, RoleService>();
            services.AddScoped<ICustomerRoleService, CustomerRoleService>();

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

            // Payment (audit repo barcha API uchun ochiq;
            // PaymentService va IPaymeClient esa AddPaymeClient orqali alohida ulanadi)
            services.AddScoped<IPaymentTransactionRepository, PaymentTransactionRepository>();

            // Hold invoice repo'lari — faqat DbContext'ga bog'liq, AdminApi ham ishlatadi.
            services.AddScoped<IPaymentSessionRepository, PaymentSessionRepository>();
            services.AddScoped<IHoldInvoiceRepository, HoldInvoiceRepository>();
            // Operator hold boshqaruvi — Payme'ni chaqirmaydi, faqat repo'lar (AdminApi'da ishlatiladi).
            services.AddScoped<IHoldInvoiceAdminService, HoldInvoiceAdminService>();

            // Reporting
            services.AddScoped<IUsageReportRepository, UsageReportRepository>();
            services.AddScoped<IUsageReportService, UsageReportService>();
            services.AddSingleton<IExcelReportExporter, ClosedXmlReportExporter>();

            return services;
        }

        /// <summary>
        /// AuthApi uchun autentifikatsiya servislari (login/OTP/refresh).
        /// AuthService IRefreshTokenStore'ga bog'liq, shu sabab <see cref="AddRedisServices"/>
        /// ham AuthApi'da chaqirilishi shart.
        /// </summary>
        public static IServiceCollection RegisterAuthServices(this IServiceCollection services, IConfiguration config)
        {
            // Imzolash tekshirish bilan bir xil secret'dan foydalanadi (GetJwtSecret — yagona manba).
            services.AddSingleton(new Domain.Auth.JwtSettings { Secret = GetJwtSecret(config) });

            // OTP: test kodi (123456) faqat config ruxsat bersa (Development) ishlaydi.
            services.AddSingleton(new Domain.Auth.OtpSettings
            {
                AllowTestCode = bool.TryParse(config["Otp:AllowTestCode"], out var allow) && allow,
                TtlMinutes = int.TryParse(config["Otp:TtlMinutes"], out var ttl) ? ttl : 3,
                MaxAttempts = int.TryParse(config["Otp:MaxAttempts"], out var max) ? max : 5
            });

            services.AddScoped<IAuthService, AuthService>();
            services.AddScoped<IPlatformAuthService, PlatformAuthService>();
            services.AddScoped<ITokenService, TokenService>();
            services.AddSingleton<IOtpService, OtpService>();

            // Auth servislar repository larga bog'liq (CommonConfiguration AuthApi'da
            // RegisterServices chaqirmasligi mumkin, shuning uchun shu yerda ham ro'yxatga olamiz).
            services.AddScoped<IPlatformUserRepository, PlatformUserRepository>();
            services.AddScoped<ICustomerUserRepository, CustomerUserRepository>();
            services.AddScoped<IPlatformRoleRepository, PlatformRoleRepository>();
            services.AddScoped<ICustomerRoleRepository, CustomerRoleRepository>();

            return services;
        }

        /// <summary>
        /// SessionApi uchun sessiya bilan bog'liq servislar.
        /// SessionService, repository lar, BootstrapService, idle session cleaner va
        /// MQTT connect oqimini boshqaruvchi <c>DeviceSessionService</c>.
        /// </summary>
        public static IServiceCollection RegisterSessionServices(this IServiceCollection services)
        {
            services.AddScoped<ICustomerUserRepository, CustomerUserRepository>();
            services.AddScoped<IDeviceRepository, DeviceRepository>();
            services.AddScoped<IProductRepository, ProductRepository>();
            services.AddScoped<IProductProcessRepository, ProductProcessRepository>();
            services.AddScoped<ISessionRepository, SessionRepository>();
            services.AddScoped<IProcessService, ProcessService>();
            services.AddScoped<ISessionService, SessionService>();
            services.AddScoped<IDeviceStatusService, DeviceStatusService>();
            services.AddScoped<IBootstrapService, BootstrapService>();
            services.AddSingleton<IPushNotificationService, LoggingPushNotificationService>();
            // ISessionNotifier va IDeviceSessionService — SessionApi Program.cs da
            // ro'yxatdan o'tkaziladi (ular SessionApi'ga bog'liq).
            services.AddHostedService<IdleSessionCleanerService>();

            return services;
        }

        /// <summary>
        /// DeviceApi uchun qurilma repository'si. DeviceApi endi faqat HTTP REST
        /// (qurilma autentifikatsiyasi, CRUD) bilan ishlaydi — MQTT/sessiya logikasi
        /// SessionApi'ga ko'chirilgan.
        /// </summary>
        public static IServiceCollection RegisterDeviceServices(this IServiceCollection services)
        {
            services.AddScoped<IDeviceRepository, DeviceRepository>();

            return services;
        }

        /// <summary>
        /// Payme Receipts API uchun typed HttpClient + PaymentService.
        /// PaymentApi va SessionApi'da chaqiriladi (boshqa API'larga keraksiz, ValidateOnBuild xatosini
        /// keltirib chiqarishi mumkin chunki PaymentService IPaymeClient'ga bog'liq).
        /// IPaymentTransactionRepository RegisterServices'da ro'yxatga olinishi shart.
        /// </summary>
        public static IServiceCollection AddPaymeClient(this IServiceCollection services, IConfiguration config)
        {
            services.Configure<PaymeOptions>(config.GetSection("Payme"));
            services.AddHttpClient<IPaymeClient, PaymeClient>();
            services.AddScoped<IPaymentService, PaymentService>();
            services.AddScoped<IPaymeCredentialResolver, PaymeCredentialResolver>();

            return services;
        }

        /// <summary>
        /// Hold invoice (Payme pre-authorization) oqimi — FAQAT SessionApi.
        /// Servislar ISessionNotifier/IDeviceCommandPublisher'ga bog'liq bo'lgani uchun
        /// boshqa API'larga qo'shilsa ValidateOnBuild yiqiladi.
        /// <see cref="AddPaymeClient"/> dan keyin chaqirilishi kerak.
        /// </summary>
        public static IServiceCollection RegisterHoldInvoiceServices(this IServiceCollection services, IConfiguration config)
        {
            services.Configure<Domain.Options.HoldInvoiceOptions>(config.GetSection("HoldInvoices"));

            services.AddScoped<IPaymentSessionService, PaymentSessionService>();
            services.AddScoped<IHoldInvoiceService, HoldInvoiceService>();
            services.AddScoped<IHoldSettlementService, HoldSettlementService>();
            services.AddScoped<IProcessSettlementService, ProcessSettlementService>();

            services.AddHostedService<Application.BackgroundServices.HoldInvoiceWatcherService>();

            return services;
        }
    }
}
