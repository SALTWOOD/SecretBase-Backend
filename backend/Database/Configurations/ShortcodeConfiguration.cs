using backend.Database.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace backend.Database.Configurations;

public class ShortcodeConfiguration : IEntityTypeConfiguration<Shortcode>
{
    public void Configure(EntityTypeBuilder<Shortcode> builder)
    {
        builder.ToTable("shortcodes");
        
        builder.HasKey(e => e.Id);

        builder.HasIndex(e => e.Name).IsUnique();

        builder.Property(e => e.Name)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(e => e.DisplayName)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(e => e.Description)
            .HasMaxLength(1000);

        builder.Property(e => e.FrontendCode)
            .IsRequired();

        builder.Property(e => e.BackendCode)
            .IsRequired();

        builder.Property(e => e.AllowedRoles)
            .HasColumnType("text[]");

        builder.HasOne(e => e.CreatedBy)
            .WithMany()
            .HasForeignKey(e => e.CreatedByUserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}