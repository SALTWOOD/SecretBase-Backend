using backend.Database.Entities;
using Microsoft.EntityFrameworkCore;

namespace backend.Database;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<User> Users => Set<User>();
    public DbSet<Invite> Invites => Set<Invite>();
    public DbSet<Setting> Settings => Set<Setting>();
    public DbSet<UserCredential> UserCredentials => Set<UserCredential>();
    public DbSet<Article> Articles => Set<Article>();
    public DbSet<Comment> Comments => Set<Comment>();
    public DbSet<Shortcode> Shortcodes => Set<Shortcode>();
    public DbSet<Entities.FileShare> FileShares => Set<Entities.FileShare>();
    public DbSet<StickerSet> StickerSets => Set<StickerSet>();
    public DbSet<Sticker> Stickers => Set<Sticker>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);

        modelBuilder.UseOpenIddict();

        modelBuilder.Entity<User>(entity =>
        {
            entity.OwnsOne(u => u.LastLoginInfo, builder => { builder.ToJson(); });
        });
    }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        // You can enable some global behaviors here
        // For example: enable sensitive data logging in development environment (use with caution!)
        // optionsBuilder.EnableSensitiveDataLogging();

        // Use with EFCore.NamingConventions plugin to implement Snake Case (users_table)
        // optionsBuilder.UseSnakeCaseNamingConvention();
    }
}