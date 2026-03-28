using backend.Database.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace backend.Database.Configurations;

public class FileShareConfiguration : IEntityTypeConfiguration<Entities.FileShare>
{
    public void Configure(EntityTypeBuilder<Entities.FileShare> entity)
    {
        entity.HasKey(e => e.ShortId);

        entity.HasIndex(e => e.OwnerId);

        entity.HasIndex(e => e.CreatedAt);

        entity.Property(e => e.ShortId)
            .IsRequired()
            .HasMaxLength(32);

        entity.Property(e => e.Bucket)
            .IsRequired()
            .HasMaxLength(255);

        entity.Property(e => e.Key)
            .IsRequired()
            .HasMaxLength(1024);

        entity.Property(e => e.FileName)
            .IsRequired()
            .HasMaxLength(255);

        entity.HasOne(e => e.Owner)
            .WithMany()
            .HasForeignKey(e => e.OwnerId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
