using backend.Database.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace backend.Database.Configurations;

public class FileShareConfiguration : IEntityTypeConfiguration<Entities.FileShare>
{
    public void Configure(EntityTypeBuilder<Entities.FileShare> builder)
    {
        builder.ToTable("file_shares");
        
        builder.HasKey(e => e.ShortId);

        builder.HasIndex(e => e.OwnerId);

        builder.HasIndex(e => e.CreatedAt);

        builder.Property(e => e.ShortId)
            .IsRequired()
            .HasMaxLength(32);

        builder.Property(e => e.Bucket)
            .IsRequired()
            .HasMaxLength(255);

        builder.Property(e => e.Key)
            .IsRequired()
            .HasMaxLength(1024);

        builder.Property(e => e.FileName)
            .IsRequired()
            .HasMaxLength(255);

        builder.Property(e => e.Remarks)
            .HasMaxLength(128);

        builder.HasOne(e => e.Owner)
            .WithMany()
            .HasForeignKey(e => e.OwnerId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}