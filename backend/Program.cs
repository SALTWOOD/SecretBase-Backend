using backend.Filters;
using backend.Services;
using Microsoft.AspNetCore.Authentication;
using SqlSugar;
using System.Threading.RateLimiting;

namespace backend;

public class Program
{
    public static async Task Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        #region Core Services Configuration
        builder.Services.AddControllers(options =>
        {
            options.Filters.Add<CaptchaFilter>();
        });

        builder.Services.AddOpenApi();
        builder.Services.AddHttpClient();
        builder.Services.AddMemoryCache();
        #endregion

        #region Database Configuration (SqlSugar)
        StaticConfig.AppContext_ConvertInfinityDateTime = true;
        var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
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
                if (value is not DateTime dt) return;
                switch (dt.Kind)
                {
                    case DateTimeKind.Unspecified:
                        entity.SetValue(DateTime.SpecifyKind(dt, DateTimeKind.Utc));
                        break;
                    case DateTimeKind.Utc:
                        break;
                    default:
                        entity.SetValue(dt.ToUniversalTime());
                        break;
                }
            };
        }));
        #endregion

        #region Custom Business Services
        builder.Services.AddScoped<ICapValidateService, CapValidateService>();
        builder.Services.AddScoped<JwtService>();
        builder.Services.AddScoped<UserService>();
        builder.Services.AddScoped<SettingService>();
        #endregion

        #region Authentication & Authorization
        builder.Services.AddAuthentication("SimpleCookie")
            .AddScheme<AuthenticationSchemeOptions, CookieAuthenticator>("SimpleCookie", null);

        builder.Services.AddAuthorization(options =>
        {
            options.AddPolicy("AdminOnly", policy => policy.RequireRole("Admin"));
        });
        #endregion

        #region Global Rate Limiting
        builder.Services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.OnRejected = async (context, _) =>
            {
                await context.HttpContext.Response.WriteAsJsonAsync(new
                {
                    message = "Too many requests. Please try again later."
                });
            };

            // Global IP-based rate limit
            options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
            {
                var ip = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
                return RateLimitPartition.GetFixedWindowLimiter(ip, _ => new FixedWindowRateLimiterOptions
                {
                    Window = TimeSpan.FromMinutes(1),
                    PermitLimit = 60,
                    QueueLimit = 0
                });
            });
        });

        var app = builder.Build();
        #endregion

        #region Database Initialization
        using (var scope = app.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ISqlSugarClient>();
            try
            {
                // Sync schemas
                db.CodeFirst.InitTables(
                    typeof(Tables.UserTable),
                    typeof(Tables.InviteTable),
                    typeof(Tables.SettingTable)
                );
                // Seed data
                await DatabaseInitializer.InitializeAsync(db);
            }
            catch (Exception ex)
            {
                var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
                logger.LogError(ex, "An error occurred during database initialization.");
            }
        }
        #endregion

        #region HTTP Pipeline
        if (app.Environment.IsDevelopment())
        {
            app.MapOpenApi();
        }

        app.UseRateLimiter();
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapControllers();
        #endregion

        await app.RunAsync();
    }
}