using backend.Database.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace backend.Database.Configurations;

public class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("users");

        builder.HasKey(x => x.Id);

        builder.HasIndex(x => x.Username).IsUnique();
        builder.HasIndex(x => x.Email).IsUnique();

        builder.Property(x => x.Username).HasMaxLength(100).IsRequired();
        builder.Property(x => x.Email).HasMaxLength(255).IsRequired();

        builder.Property(x => x.Role)
               .HasConversion<string>()
               .HasMaxLength(20);

        builder.OwnsOne(x => x.LastLoginInfo, info =>
        {
            info.ToJson();
        });

        builder.Property(x => x.TotpRecoveryCodes)
               .HasColumnType("text[]");

        builder.Property(x => x.RegisterTime)
               .HasColumnType("timestamptz")
               .HasDefaultValueSql("CURRENT_TIMESTAMP");

        // MyIssuedInvites 关系在 InviteConfiguration 中配置
    }
}