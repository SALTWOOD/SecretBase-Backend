using backend.Database.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace backend.Database.Configurations;

public class ThirdPartyBindingConfiguration : IEntityTypeConfiguration<ThirdPartyBinding>
{
    public void Configure(EntityTypeBuilder<ThirdPartyBinding> builder)
    {
        builder.ToTable("third_party_bindings");

        builder.HasKey(x => x.Id);

        builder.HasIndex(x => new { x.Provider, x.ProviderUserId })
            .IsUnique()
            .HasDatabaseName("ix_third_party_bindings_provider_userid");

        builder.HasIndex(x => x.UserId)
            .HasDatabaseName("ix_third_party_bindings_userid");

        builder.Property(x => x.Provider)
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(x => x.ProviderUserId)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(x => x.ProviderUsername)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(x => x.ProviderAvatarUrl)
            .HasMaxLength(500);

        builder.Property(x => x.AccessToken)
            .HasMaxLength(500);

        builder.Property(x => x.CreatedAt)
            .HasColumnType("timestamptz")
            .HasDefaultValueSql("CURRENT_TIMESTAMP");

        builder.HasOne(x => x.User)
            .WithMany()
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
