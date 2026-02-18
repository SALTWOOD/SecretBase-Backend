using backend;
using backend.Controllers;
using backend.Database;
using backend.Database.Entities;
using backend.Filters;
using backend.Middleware;
using backend.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using OpenIddict.Abstractions;
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

// 添加 CORS 配置
builder.Services.AddCors(options =>
{
    options.AddPolicy("DefaultCorsPolicy", policy =>
    {
        var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>();
        if (allowedOrigins != null && allowedOrigins.Length > 0)
        {
            policy.WithOrigins(allowedOrigins)
                  .AllowAnyMethod()
                  .AllowAnyHeader()
                  .AllowCredentials();
        }
    });
});
#endregion

#region Database (EF Core with PostgreSQL)
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");

builder.Services.AddDbContext<AppDbContext>(options =>
{
    // Configure PostgreSQL
    options.UseNpgsql(connectionString);

    // Register OpenIddict entities
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

    // Add a stricter rate limit policy for OAuth token endpoint
    options.AddPolicy("TokenEndpoint", context =>
    {
        var ip = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        return RateLimitPartition.GetFixedWindowLimiter(ip, _ => new FixedWindowRateLimiterOptions
        {
            Window = TimeSpan.FromMinutes(1),
            PermitLimit = 20, // Stricter limit for token endpoint
            QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
            QueueLimit = 5
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
               .SetTokenEndpointUris("/connect/token")
               .SetIntrospectionEndpointUris("/connect/introspect")
               .SetRevocationEndpointUris("/connect/revoke");

        // Enable authorization code flow with PKCE support
        options.AllowAuthorizationCodeFlow();

        // Enable refresh token flow
        options.AllowRefreshTokenFlow();
        options.DisableAccessTokenEncryption();

        // Configure token lifetimes
        options.SetAuthorizationCodeLifetime(TimeSpan.FromMinutes(5))
               .SetAccessTokenLifetime(TimeSpan.FromMinutes(60))
               .SetRefreshTokenLifetime(TimeSpan.FromDays(30));

        // Register standard scopes
        options.RegisterScopes(
            OpenIddictConstants.Permissions.Scopes.Email,
            OpenIddictConstants.Permissions.Scopes.Profile,
            OpenIddictConstants.Permissions.Prefixes.Scope + "offline_access"
        );

        // Register custom scopes
        options.RegisterScopes(
            "roles",
            "apps",
            "consents",
            "tokens",
            "settings:read",
            "settings:write",
            "invites:read",
            "invites:write"
        );

        // Add development certificates (use production certificates in production!)
        options.AddDevelopmentEncryptionCertificate()
               .AddDevelopmentSigningCertificate()
               .SetIssuer(builder.Configuration["OpenIddict:Issuer"].ThrowIfNull());

        // Configure ASP.NET Core integration
        options.UseAspNetCore()
               .DisableTransportSecurityRequirement()
               .EnableTokenEndpointPassthrough()
               .EnableAuthorizationEndpointPassthrough()
               .EnableStatusCodePagesIntegration();
    });
#endregion

var app = builder.Build();

#region Middleware Pipeline
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.UseRequestLogging();
}

app.UseCors("DefaultCorsPolicy");
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
