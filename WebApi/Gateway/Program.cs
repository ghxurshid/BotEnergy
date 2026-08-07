using System.Security.Claims;
using CommonConfiguration.ConfigurationExtensions;
using CommonConfiguration.ConfigurationServices;
using CommonConfiguration.Observability;
using Gateway.Extensions;
using Gateway.Middlewares;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Yarp.ReverseProxy.Transforms;

var builder = WebApplication.CreateBuilder(args);
builder.AddBotEnergyLogging("Gateway");
builder.AddValidatedServiceProvider();
builder.Configuration.AddCommonConfiguration();

// Gateway REST (HTTP/1.1) va SignalR/WebSocket'ni bitta plain portda multiplekslaydi.
// TLS'ni oldindagi Nginx terminate qiladi — bu yerda hech qanday sertifikat kerak emas.
var gatewayPort = builder.Configuration.GetValue("Hosting:Ports:Gateway", 8080);
builder.WebHost.ConfigureKestrel(options =>
{
    options.ListenAnyIP(gatewayPort, listenOptions =>
    {
        listenOptions.Protocols = HttpProtocols.Http1AndHttp2;
    });
});

// --- Reverse proxy: route/cluster jadvali Configuration.json'dagi "ReverseProxy" bo'limidan ---
builder.Services
    .AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"))
    .AddTransforms(context =>
    {
        context.AddRequestTransform(transformContext =>
        {
            // Correlation ID — log, trace va downstream servis o'rtasida bir xil qiymat.
            transformContext.ProxyRequest.Headers.TryAddWithoutValidation(
                "X-Request-Id", transformContext.HttpContext.TraceIdentifier);

            // Identity konteksti downstream audit uchun. Bu ISHONCH manbai emas —
            // servis baribir JWT'ni o'zi tekshiradi va PermissionFilter'ni qo'llaydi.
            var user = transformContext.HttpContext.User;
            if (user?.Identity?.IsAuthenticated != true)
                return ValueTask.CompletedTask;

            Forward(ClaimTypes.NameIdentifier, "X-User-Id");
            Forward("UserGroup", "X-User-Group");
            Forward("UserSubType", "X-User-SubType");
            Forward("MerchantId", "X-Merchant-Id");
            Forward("OrganizationId", "X-Organization-Id");

            return ValueTask.CompletedTask;

            void Forward(string claimType, string headerName)
            {
                var value = user.FindFirst(claimType)?.Value;
                if (!string.IsNullOrEmpty(value))
                    transformContext.ProxyRequest.Headers.TryAddWithoutValidation(headerName, value);
            }
        });
    });

// Gateway ikkala audience'ni ham qabul qiladi (Customer + Platform) — qaysi API qaysi
// guruhga ochiqligini servisning o'zi hal qiladi (AdminApi platform-only va h.k.).
builder.Services.AddJwtAuthentication(builder.Configuration, signalRHubPath: "/hubs");
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("authenticated", policy => policy.RequireAuthenticatedUser());
});

builder.Services.AddGatewayRateLimiting(builder.Configuration);
builder.Services.AddSimulatorCors(builder.Configuration);
builder.Services.AddProxyForwardedHeaders();
builder.Services.AddBotEnergyObservability(builder.Configuration, "Gateway");
builder.Services.AddHealthChecks();

var app = builder.Build();

app.Urls.Clear();

// Nginx ortida haqiqiy klient IP'sini tiklaydi — rate limiting'ning to'g'ri ishlashi shunga bog'liq.
app.UseProxyForwardedHeaders();

app.UseMiddleware<AuditLoggingMiddleware>();
app.UseSimulatorCors();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

app.MapHealthChecks("/health/live");
app.MapHealthChecks("/health/ready");
app.MapBotEnergyMetrics();
app.MapGatewaySwaggerUi();
app.MapReverseProxy();

app.Run();
