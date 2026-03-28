using backend.Database.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace backend.Database.Configurations;

public class ShortcodeConfiguration : IEntityTypeConfiguration<Shortcode>
{
    public void Configure(EntityTypeBuilder<Shortcode> entity)
    {
        entity.HasKey(e => e.Id);

        entity.HasIndex(e => e.Name).IsUnique();

        entity.Property(e => e.Name)
            .IsRequired()
            .HasMaxLength(100);

        entity.Property(e => e.DisplayName)
            .IsRequired()
            .HasMaxLength(200);

        entity.Property(e => e.Description)
            .HasMaxLength(1000);

        entity.Property(e => e.FrontendCode)
            .IsRequired();

        entity.Property(e => e.BackendCode)
            .IsRequired();

        entity.Property(e => e.AllowedRoles)
            .HasColumnType("text[]");

        entity.HasOne(e => e.CreatedBy)
            .WithMany()
            .HasForeignKey(e => e.CreatedByUserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}