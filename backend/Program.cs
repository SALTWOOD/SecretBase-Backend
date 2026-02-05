using backend;
using backend.Filters;
using backend.Services;
using backend.Tables;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using SqlSugar;
using StackExchange.Redis;
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

#region Database (SqlSugar)
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
StaticConfig.AppContext_ConvertInfinityDateTime = true;
builder.Services.AddScoped<ISqlSugarClient>(_ => new SqlSugarClient(new ConnectionConfig
{
    ConnectionString = connectionString,
    DbType = DbType.PostgreSQL,
    IsAutoCloseConnection = true,
    InitKeyType = InitKeyType.Attribute
}, db =>
{
    db.Aop.DataExecuting = (value, entity) =>
    {
        if (value is DateTime dt)
        {
            entity.SetValue(dt.Kind == DateTimeKind.Unspecified
                ? DateTime.SpecifyKind(dt, DateTimeKind.Utc)
                : dt.ToUniversalTime());
        }
    };
}));
#endregion

#region Redis (Dragonfly)
var redisConnection = builder.Configuration.GetConnectionString("RedisConnection")
    ?? throw new InvalidOperationException("Redis connection string is missing.");

builder.Services.AddSingleton<IConnectionMultiplexer>(_ => ConnectionMultiplexer.Connect(redisConnection));
#endregion

#region Business Services
builder.Services.AddScoped<ICapValidateService, CapValidateService>();
builder.Services.AddScoped<SessionService>();
builder.Services.AddScoped<SettingService>();
builder.Services.AddScoped<BaseServices>();
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
    try
    {
        var db = services.GetRequiredService<ISqlSugarClient>();
        db.CodeFirst.InitTables(
            typeof(backend.Tables.UserTable),
            typeof(backend.Tables.InviteTable),
            typeof(backend.Tables.SettingTable)
        );
        await DatabaseInitializer.InitializeAsync(db);
    }
    catch (Exception ex)
    {
        var logger = services.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "Database init failed.");
    }
}
#endregion

await app.RunAsync();