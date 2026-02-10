using backend;
using backend.Controllers;
using backend.Database;
using backend.Database.Entities;
using backend.Filters;
using backend.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using StackExchange.Redis;
using System;
using System.Threading.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

#region Framework Services
builder.Services.AddControllers(options =>
{
    options.Filters.Add<CaptchaFilter>();
});
builder.Services.AddOpenApi();
builder.Services.AddHttpClient();
builder.Services.AddMemoryCache();
#endregion

#region Database (EF Core with PostgreSQL)
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");

builder.Services.AddDbContext<AppDbContext>(options =>
{
    // 配置 PostgreSQL
    options.UseNpgsql(connectionString);

    // 注册 OpenIddict 实体
    options.UseOpenIddict();
});

#endregion

#region Redis (Dragonfly)
var redisConnection = builder.Configuration.GetConnectionString("RedisConnection")
    ?? throw new InvalidOperationException("Redis connection string is missing.");

builder.Services.AddSingleton<IConnectionMultiplexer>(_ => ConnectionMultiplexer.Connect(redisConnection));
#endregion

#region Business Services
builder.Services.AddSingleton<TwoFactorManager>();
builder.Services.AddScoped<ICapValidateService, CapValidateService>();
builder.Services.AddScoped<SessionService>();
builder.Services.AddScoped<SettingService>();
builder.Services.AddScoped<BaseServices>();
builder.Services.AddScoped<WebAuthnService>();
builder.Services.AddScoped<TwoFactorFilter>();
builder.Services.AddFido2(options =>
{
    options.ServerDomain = builder.Configuration["WebAuthn:ServerDomain"] ?? "localhost";
    options.ServerName = "SecretBase";
    options.Origins = new HashSet<string> { builder.Configuration["WebAuthn:Origin"]! };
    options.TimestampDriftTolerance = 300000;
});
#endregion

#region Auth & Rate Limiter
builder.Services.AddSingleton<IAuthorizationHandler, MinimumRoleHandler>();
builder.Services.AddAuthentication("SimpleSession")
    .AddScheme<AuthenticationSchemeOptions, CookieAuthenticator>("SimpleSession", null);

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("AdminOnly", policy =>
        policy.AddRequirements(new MinimumRoleRequirement(UserRole.Admin)));
});

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.OnRejected = async (context, _) =>
    {
        await context.HttpContext.Response.WriteAsJsonAsync(new { message = "Too many requests." });
    };
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
    {
        var ip = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        return RateLimitPartition.GetFixedWindowLimiter(ip, _ => new FixedWindowRateLimiterOptions
        {
            Window = TimeSpan.FromMinutes(1),
            PermitLimit = 60
        });
    });
});
#endregion

#region OAuth (OpenIddict with EF Core)
builder.Services.AddOpenIddict()
    .AddCore(options =>
    {
        options.UseEntityFrameworkCore()
               .UseDbContext<AppDbContext>();
    })
    .AddServer(options =>
    {
        options.SetAuthorizationEndpointUris("/connect/authorize")
               .SetTokenEndpointUris("/connect/token");

        options.AllowAuthorizationCodeFlow();

        options.AddDevelopmentEncryptionCertificate()
               .AddDevelopmentSigningCertificate();

        options.UseAspNetCore()
               .EnableTokenEndpointPassthrough()
               .EnableAuthorizationEndpointPassthrough();
    });
#endregion

var app = builder.Build();

#region Middleware Pipeline
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
#endregion

#region Data Seeding & Migration
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    var context = services.GetRequiredService<AppDbContext>();
    var setting = services.GetRequiredService<SettingService>();

    await DatabaseInitializer.InitializeAsync(context, setting);
}
#endregion

await app.RunAsync();
