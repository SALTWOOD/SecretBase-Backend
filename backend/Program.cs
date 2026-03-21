using backend;
using backend.Filters;
using backend.Middleware;
using backend.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using System.Threading.RateLimiting;
using backend.Database.Models;
using Supabase;

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
        {
            policy.WithOrigins(allowedOrigins)
                .AllowAnyMethod()
                .AllowAnyHeader()
                .AllowCredentials();
        }
    });
});

#endregion

#region Database (Supabase)

var url = builder.Configuration["Supabase:Url"];
var key = builder.Configuration["Supabase:Key"];

builder.Services.AddScoped<Supabase.Client>(_ =>
    new Supabase.Client(url, key, new SupabaseOptions
    {
        AutoConnectRealtime = true
    }));

SettingRegistry.Provider = new SettingProvider(new Supabase.Client(url, key, new SupabaseOptions
{
    AutoConnectRealtime = true
}));

#endregion

#region Auth & Rate Limiter

builder.Services.AddSingleton<IAuthorizationHandler, MinimumRoleHandler>();

// 配置双认证方案：Cookie Session + OAuth Bearer
builder.Services.AddAuthentication("SimpleSession")
    // .AddScheme<AuthenticationSchemeOptions, CookieAuthenticator>("SimpleSession", null)
    .AddScheme<AuthenticationSchemeOptions, OAuthBearerAuthenticator>(OAuthBearerAuthenticator.SchemeName, null);

builder.Services.AddAuthorization(options =>
{
    // 默认策略：支持任一认证方式
    options.DefaultPolicy = new Microsoft.AspNetCore.Authorization.AuthorizationPolicyBuilder()
        .AddAuthenticationSchemes("SimpleSession", OAuthBearerAuthenticator.SchemeName)
        .RequireAuthenticatedUser()
        .Build();

    // CookieOnly 策略：仅限 Cookie Session 认证（用于敏感操作）
    options.AddPolicy("CookieOnly", policy =>
        policy.AddAuthenticationSchemes("SimpleSession")
            .RequireAuthenticatedUser());

    // OAuthOnly 策略：仅限 OAuth Bearer 认证
    options.AddPolicy("OAuthOnly", policy =>
        policy.AddAuthenticationSchemes(OAuthBearerAuthenticator.SchemeName)
            .RequireAuthenticatedUser());

    // AdminOnly 策略：需要 Admin 角色（仅 Cookie Session）
    options.AddPolicy("AdminOnly", policy =>
        policy.AddAuthenticationSchemes("SimpleSession")
            .AddRequirements(new MinimumRoleRequirement(UserRole.Admin)));
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

await DatabaseInitializer.InitializeAsync();

#endregion

await app.RunAsync();