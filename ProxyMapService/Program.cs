using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using ProxyMapService.Interfaces;
using ProxyMapService.Middleware;
using ProxyMapService.Models;
using ProxyMapService.Proxy.Counters;
using ProxyMapService.Services;
using ProxyMapService.Utils;
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
var devTokenConfig = builder.Configuration.GetSection("Authentication:DevToken");
var devTokenEnabled = devTokenConfig.GetValue<bool>("Enabled");
var serveDashboard = builder.Configuration.GetValue<bool>("Dashboard:Enabled");

bool isRunningUnderIis = !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("APP_POOL_ID")) ||
                         !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("ANCM_LAUNCH_DATA"));

string routePrefix = isRunningUnderIis ? "" : "/ProxyMapService";

string hubPath = $"{routePrefix}/updates";

var AllowAllCors = "AllowAllCors";

var allowedOrigins = builder.Configuration
    .GetSection("CorsSettings:AllowedOrigins")
    .Get<string[]>();

Console.OutputEncoding = Encoding.UTF8;

// Add services to the container.

builder.Services.AddSignalR();

var monitoringOptions = builder.Configuration
    .GetSection("WebSocketMonitoring")
    .Get<WebSocketMonitoringOptions>();

var loggingSwitch = new LoggingSwitch
{
    IsEventCapture = monitoringOptions?.EventLog?.Capture ?? false,
    IsHttpCapture = monitoringOptions?.TrafficMonitor?.Capture ?? false
};

builder.Services.AddSingleton(loggingSwitch);
builder.Services.AddSingleton<IEventLoggingSwitch>(sp => sp.GetRequiredService<LoggingSwitch>());
builder.Services.AddSingleton<IHttpLoggingSwitch>(sp => sp.GetRequiredService<LoggingSwitch>());

builder.Services.AddSingleton<WebSocketLogBackgroundService>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<WebSocketLogBackgroundService>());

builder.Services.AddSingleton<IHttpTrafficMonitor, HttpTrafficMonitor>();

builder.Services.AddSingleton<IProxyService, ProxyService>();
builder.Services.AddHostedService<ProxyBackgroundService>();

builder.Services.AddSingleton<ILogStorage, MemoryLogStorage>();
builder.Services.AddSingleton<IHttpTrafficStorage, MemoryTrafficStorage>();

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

string? devToken = null;

if (jwtAuthEnabled)
{
    builder.Services
        .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
        .AddJwtBearer(options =>
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
                    if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments(hubPath))
                    {
                        context.Token = accessToken;
                    }
                    return Task.CompletedTask;
                }
            };
        });
}
else if (devTokenEnabled)
{
    devToken = Guid.NewGuid().ToString("N");
    builder.Services.AddSingleton(new DevTokenProvider(devToken));
    builder.Services
        .AddAuthentication("DevTokenScheme")
        .AddScheme<AuthenticationSchemeOptions, DevTokenAuthHandler>("DevTokenScheme", null);
}

builder.Services.AddAuthorization(options =>
{
    if (jwtAuthEnabled)
    {
        options.DefaultPolicy = new AuthorizationPolicyBuilder()
            .AddAuthenticationSchemes(JwtBearerDefaults.AuthenticationScheme)
            .RequireAuthenticatedUser()
            .Build();
    }
    else if (devTokenEnabled)
    {
        options.DefaultPolicy = new AuthorizationPolicyBuilder()
            .AddAuthenticationSchemes("DevTokenScheme")
            .RequireAuthenticatedUser()
            .Build();
    }
    else
    {
        options.DefaultPolicy = new AuthorizationPolicyBuilder()
            .RequireAssertion(_ => true)
            .Build();
    }
});

builder.Services.Configure<WebSocketMonitoringOptions>(builder.Configuration.GetSection("WebSocketMonitoring"));

builder.Logging.AddConfiguration(builder.Configuration.GetSection("Logging"));
builder.Services.TryAddEnumerable(ServiceDescriptor.Singleton<ILoggerProvider, WebSocketLoggerProvider>());

builder.Services.AddControllers(options =>
{
    if (!isRunningUnderIis)
    {
        options.Conventions.Add(new GlobalRoutePrefixConvention("ProxyMapService"));
    }
});

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
    if (Environment.GetEnvironmentVariable("START_VITE") == "true")
    {
        ViteLauncher.StartIfNeeded();
    }
}

app.UseCors(AllowAllCors);

app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/", () => "Hello World!");

if (serveDashboard)
{
    app.UseStaticFiles();
    app.MapFallbackToFile("/ProxyMapDashboard/{*path:nonfile}", "ProxyMapDashboard/index.html");
}

app.MapControllers();

app.MapHub<LogHub>(hubPath);

bool shouldLaunch = Environment.GetEnvironmentVariable("LAUNCH_DASHBOARD_IN_BROWSER") == "true";
if (shouldLaunch || devToken != null)
{
    if (devToken != null)
    {
        app.Lifetime.ApplicationStarted.Register(() =>
        {
            var loggerFactory = app.Services.GetRequiredService<ILoggerFactory>();
            var logger = loggerFactory.CreateLogger("DevTokenLauncher");
            var server = app.Services.GetRequiredService<IServer>();
            var devTokenProvider = app.Services.GetRequiredService<DevTokenProvider>();

            var addresses = server.Features.Get<IServerAddressesFeature>()?.Addresses;

            if (addresses != null && addresses.Count != 0)
            {
                logger.LogWarning("DEVELOPMENT TOKEN GENERATED: {Token}", devTokenProvider.Token);

                string targetUrl = "";

                if (Environment.GetEnvironmentVariable("START_VITE") == "true")
                {
                    targetUrl = $"http://localhost:5173/ProxyMapDashboard/?token={devTokenProvider.Token}";
                }
                else
                {
                    var primaryAddress = addresses.First().TrimEnd('/');
                    targetUrl = $"{primaryAddress}/ProxyMapDashboard/?token={devTokenProvider.Token}";
                }

                logger.LogWarning("Dashboard URL: {url}", targetUrl);

                if (shouldLaunch)
                {
                    BrowserLauncher.OpenBrowser(targetUrl);
                }
            }
        });
    }
    else
    {
        app.Lifetime.ApplicationStarted.Register(() =>
        {
            var server = app.Services.GetRequiredService<IServer>();
            var addresses = server.Features.Get<IServerAddressesFeature>()?.Addresses;

            if (addresses != null && addresses.Count != 0)
            {
                string targetUrl = "";

                if (Environment.GetEnvironmentVariable("START_VITE") == "true")
                {
                    targetUrl = $"http://localhost:5173/ProxyMapDashboard/";
                }
                else
                {
                    var primaryAddress = addresses.First().TrimEnd('/');
                    targetUrl = $"{primaryAddress}/ProxyMapDashboard/";
                }

                BrowserLauncher.OpenBrowser(targetUrl);
            }
        });
    }
}

app.Run();
