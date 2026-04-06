using CommonConfiguration.ConfigurationExtensions;
using CommonConfiguration.ConfigurationServices;
using CommonConfiguration.Filters;
using Domain.Interfaces;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using System.Text;
using UsageSessionApi.Hubs;
using UsageSessionApi.Mqtt;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers(options =>
{
    options.Filters.Add<PermissionFilter>();
});
builder.Services.AddSignalR();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "UsageSession API",
        Version = "v1",
        Description = "Sessiya boshqaruvi servisi — mahsulot berish sessiyasi yaratish, MQTT orqali qurilma bilan aloqa, real-time SignalR yangilanishlar"
    });
    var xmlFile = $"{System.Reflection.Assembly.GetExecutingAssembly().GetName().Name}.xml";
    options.IncludeXmlComments(Path.Combine(AppContext.BaseDirectory, xmlFile));
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
                Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
            },
            Array.Empty<string>()
        }
    });
});

builder.Configuration.AddCommonConfiguration();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.RegisterSessionServices();

builder.Services.Configure<MqttSessionOptions>(builder.Configuration.GetSection("Mqtt"));
builder.Services.AddSingleton<MqttSessionBridge>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<MqttSessionBridge>());
builder.Services.AddScoped<ISessionNotifier, SignalRSessionNotifier>();

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
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
                    builder.Configuration["Jwt:Secret"]
                    ?? "3f1e2d4c5a6b7c8d9e0f1a2b3c4d5e6f7a8b9c0d1e2f3a4b5c6d7e8f9a0b1c2d"))
        };
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                var accessToken = context.Request.Query["access_token"];
                var path = context.HttpContext.Request.Path;
                if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/hubs"))
                    context.Token = accessToken;
                return Task.CompletedTask;
            }
        };
    });

var app = builder.Build();

await app.ApplyMigrationsAsync();

app.UseCustomExceptionMiddleware();
app.UseSwagger();
app.UseSwaggerUI();
app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapHub<UsageSessionHub>("/hubs/session");

app.Run("http://*:5007");
