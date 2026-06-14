using Amazon;
using Amazon.Runtime;
using Amazon.S3;
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
using StackExchange.Redis;
using System;
using System.Threading.RateLimiting;
using backend.Services.Shortcodes;
using backend.SourceGenerators;
using backend.Types;
using ImageProxyClient;

var builder = WebApplication.CreateBuilder(args);

#region Framework Services

builder.Services.AddControllers(options => { options.Filters.Add<CaptchaFilter>(); });
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
            policy.WithOrigins(allowedOrigins)
                .AllowAnyMethod()
                .AllowAnyHeader()
                .WithExposedHeaders("x-total-count")
                .AllowCredentials();
    });
});

#endregion

#region Database (EF Core with PostgreSQL)

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");

builder.Services.AddDbContext<AppDbContext>(options =>
{
    // Configure PostgreSQL
    options.UseNpgsql(connectionString);
});

#endregion

#region Redis (Dragonfly)

var redisConnection = builder.Configuration.GetConnectionString("RedisConnection")
                      ?? throw new InvalidOperationException("Redis connection string is missing.");

builder.Services.AddSingleton<IConnectionMultiplexer>(_ => ConnectionMultiplexer.Connect(redisConnection));

#endregion

#region AWS S3 Storage

builder.Services.AddSingleton<IAmazonS3>(sp =>
{
    var configuration = sp.GetRequiredService<IConfiguration>();
    var s3Section = configuration.GetSection("S3");

    var accessKeyId = s3Section["AccessKeyId"] ?? throw new InvalidOperationException("S3:AccessKeyId is missing.");
    var secretAccessKey = s3Section["SecretAccessKey"] ??
                          throw new InvalidOperationException("S3:SecretAccessKey is missing.");
    var region = s3Section["Region"] ?? throw new InvalidOperationException("S3:Region is missing.");

    var credentials = new BasicAWSCredentials(accessKeyId, secretAccessKey);
    var config = new AmazonS3Config
    {
        RegionEndpoint = RegionEndpoint.GetBySystemName(region),
        ServiceURL = s3Section["Endpoint"],
        UseHttp = !bool.TryParse(s3Section["UseHttps"], out var useHttps) || !useHttps,
        ForcePathStyle = bool.TryParse(s3Section["ForcePathStyle"], out var forcePathStyle) && forcePathStyle
    };

    return new AmazonS3Client(credentials, config);
});

#endregion

#region ImageProxyClient

builder.Services.AddImageProxyClient(
    builder.Configuration.GetSection("Imgproxy")
);

#endregion

#region Business Services

builder.Services.AddSingleton<TwoFactorManager>();
builder.Services.AddScoped<ICapValidateService, CapValidateService>();
builder.Services.AddScoped<SessionService>();
builder.Services.AddScoped<BaseServices>();
builder.Services.AddScoped<WebAuthnService>();
builder.Services.AddScoped<TwoFactorFilter>();
builder.Services.AddScoped<ShortcodeSandbox>();
builder.Services.AddScoped<ShortcodeService>();
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

// 配置 Cookie Session 认证
builder.Services.AddAuthentication("SimpleSession")
    .AddScheme<AuthenticationSchemeOptions, CookieAuthenticator>("SimpleSession", null);

builder.Services.AddAuthorization(options =>
{
    // 默认策略：Cookie Session 认证
    options.DefaultPolicy = new AuthorizationPolicyBuilder()
        .AddAuthenticationSchemes("SimpleSession")
        .RequireAuthenticatedUser()
        .Build();

    // CookieOnly 策略：仅限 Cookie Session 认证（用于敏感操作）
    options.AddPolicy("CookieOnly", policy =>
        policy.AddAuthenticationSchemes("SimpleSession")
            .RequireAuthenticatedUser());

    // AdminOnly 策略：需要 Admin 角色（仅 Cookie Session）
    options.AddPolicy("AdminOnly", policy =>
        policy.AddAuthenticationSchemes("SimpleSession")
            .AddRequirements(new MinimumRoleRequirement(UserRole.Admin)));
});

// Load rate limiter configuration
var rateLimiterConfig = new RateLimiterOptions();
builder.Configuration.GetSection("RateLimiter").Bind(rateLimiterConfig);

if (rateLimiterConfig.Enabled)
{
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
                Window = TimeSpan.FromSeconds(rateLimiterConfig.WindowSeconds),
                PermitLimit = rateLimiterConfig.PermitLimit,
                QueueLimit = rateLimiterConfig.QueueLimit
            });
        });
    });
}

#endregion

var app = builder.Build();

#region Middleware Pipeline

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.UseRequestLogging();
}

app.UseCors("DefaultCorsPolicy");
if (rateLimiterConfig.Enabled)
{
    app.UseRateLimiter();
}
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

#endregion

#region Data Seeding & Migration

using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    var context = services.GetRequiredService<AppDbContext>();

    await DatabaseInitializer.InitializeAsync(context);
}

var scopeFactory = app.Services.GetRequiredService<IServiceScopeFactory>();
SettingNode.GlobalProvider = new EfSettingProvider(scopeFactory);

#endregion

await app.RunAsync();
