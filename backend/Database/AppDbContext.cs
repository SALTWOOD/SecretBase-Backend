using backend.Database.Entities;
using Microsoft.EntityFrameworkCore;

namespace backend.Database;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    // 定义所有的 DbSet，建议使用名词复数，实体用名词单数喵
    public DbSet<User> Users => Set<User>();
    public DbSet<Invite> Invites => Set<Invite>();
    public DbSet<Setting> Settings => Set<Setting>();
    public DbSet<UserCredential> UserCredentials => Set<UserCredential>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // 1. 自动应用当前程序集中所有实现了 IEntityTypeConfiguration<T> 的类
        // 这样你之前写的 UserConfiguration, InviteConfiguration 都会自动生效喵！
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);

        // 2. 如果你使用了 OpenIddict，请务必加上这句
        // 它会自动创建 OpenIddict 需要的 Application, Authorization, Scope, Token 等表
        modelBuilder.UseOpenIddict();

        modelBuilder.Entity<User>(entity =>
        {
            entity.OwnsOne(u => u.LastLoginInfo, builder =>
            {
                builder.ToJson();
            });
        });
    }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        // 可以在这里开启一些全局行为
        // 例如：在开发环境开启敏感数据记录 (慎用喵！)
        // optionsBuilder.EnableSensitiveDataLogging();

        // 配合 EFCore.NamingConventions 插件实现 Snake Case (users_table)
        // optionsBuilder.UseSnakeCaseNamingConvention();
    }
}