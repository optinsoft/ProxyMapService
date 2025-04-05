using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using ProxyMapService.Interfaces;
using ProxyMapService.Proxy.Configurations;
using ProxyMapService.Services;
using System.Text;
using static ProxyMapService.LocalhostMiddlware;

var builder = WebApplication.CreateBuilder(args);

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

app.MapControllers();

app.Run();
