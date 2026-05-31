using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.IdentityModel.Tokens;
using ProxyMapService.Interfaces;
using ProxyMapService.Middleware;
using ProxyMapService.Services;
using ProxyMapService.Vite;
using ProxyMapService.WebLogging;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

var logggingEventLog = builder.Configuration.GetSection("Logging:EventLog");
if (logggingEventLog.Exists())
{
#pragma warning disable CA1416 // Checking platform compatibility
    builder.Logging.AddEventLog(eventLogSettings =>
    {
        eventLogSettings.SourceName = "ProxyMapService";
    });
#pragma warning restore CA1416 // Checking platform compatibility
}

var jwtAuthConfig = builder.Configuration.GetSection("Authentication:Jwt");
var jwtAuthEnabled = jwtAuthConfig.GetValue<bool>("Enabled");

var AllowAllCors = "AllowAllCors";

var allowedOrigins = builder.Configuration
    .GetSection("CorsSettings:AllowedOrigins")
    .Get<string[]>();

Console.OutputEncoding = Encoding.UTF8;

// Add services to the container.

builder.Services.AddSignalR();

builder.Services.AddSingleton<IProxyService, ProxyService>();
builder.Services.AddHostedService<ProxyBackgroundService>();

builder.Services.AddCors(options =>
{
    options.AddPolicy(name: AllowAllCors,
        builder =>
        {
            if (allowedOrigins != null && allowedOrigins.Length > 0)
            {
                builder.WithOrigins(allowedOrigins);
            }
            builder
                .AllowAnyMethod()
                .AllowAnyHeader()
                .AllowCredentials();
        });
});

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        if (jwtAuthEnabled)
        {
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                ValidateAudience = true,
                ValidIssuer = jwtAuthConfig.GetValue<string>("Issuer"),
                ValidAudience = jwtAuthConfig.GetValue<string>("Audience"),
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtAuthConfig.GetValue<string>("Key") ?? string.Empty))
            };
            options.Events = new JwtBearerEvents
            {
                OnMessageReceived = context =>
                {
                    var accessToken = context.Request.Query["access_token"];
                    var path = context.HttpContext.Request.Path;
                    if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/EventLog"))
                    {
                        context.Token = accessToken;
                    }
                    return Task.CompletedTask;
                }
            };
        }
    });

builder.Services.Configure<WebSocketLoggerOptions>(builder.Configuration.GetSection("Logging:WebSocket"));

builder.Logging.AddConfiguration(builder.Configuration.GetSection("Logging"));
builder.Services.TryAddEnumerable(ServiceDescriptor.Singleton<ILoggerProvider, WebSocketLoggerProvider>());

builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

if (builder.Configuration.GetValue<bool>("Security:AllowOnlyLocalhost"))
{
    app.UseMiddleware<LocalhostMiddleware>();
}

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();

    ViteLauncher.StartIfNeeded();
}

app.UseCors(AllowAllCors);

app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/", () => "Hello World!");

app.MapControllers();

app.MapHub<LogHub>("/updates");

app.Run();
