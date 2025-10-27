using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using ProxyMapService.Interfaces;
using ProxyMapService.Services;
using System.Text;
using ProxyMapService.Middleware;

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

Console.OutputEncoding = Encoding.UTF8;

// Add services to the container.

builder.Services.AddSingleton<IProxyService, ProxyService>();
builder.Services.AddHostedService<ProxyBackgroundService>();

builder.Services.AddCors(options =>
{
    options.AddPolicy(name: AllowAllCors,
        builder => builder.AllowAnyOrigin()
            .AllowAnyMethod()
            .AllowAnyHeader());
});

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(options =>
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
    }
});

builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

app.UseMiddleware<LocalhostMiddleware>();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors(AllowAllCors);

app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/", () => "Hello World!");

app.MapControllers();

app.Run();
