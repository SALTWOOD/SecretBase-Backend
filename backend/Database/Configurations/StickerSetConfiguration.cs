using backend.Database.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace backend.Database.Configurations;

public class StickerSetConfiguration : IEntityTypeConfiguration<StickerSet>
{
    public void Configure(EntityTypeBuilder<StickerSet> builder)
    {
        builder.ToTable("sticker_sets");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedOnAdd();

        builder.Property(x => x.Name).HasMaxLength(100).IsRequired();
        builder.Property(x => x.CreatorId).IsRequired();

        builder.HasOne(x => x.CreatedBy)
            .WithMany()
            .HasForeignKey(x => x.CreatorId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Property(x => x.CreatedAt)
            .HasColumnType("timestamptz")
            .HasDefaultValueSql("CURRENT_TIMESTAMP");

        builder.Property(x => x.UpdatedAt)
            .HasColumnType("timestamptz")
            .HasDefaultValueSql("CURRENT_TIMESTAMP");

        builder.HasMany(x => x.Stickers)
            .WithOne(x => x.StickerSet)
            .HasForeignKey(x => x.StickerSetId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(x => x.CreatorId);
        builder.HasIndex(x => x.CreatedAt);
    }
}
