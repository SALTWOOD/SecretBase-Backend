using backend.Database.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace backend.Database.Configurations;

public class StickerConfiguration : IEntityTypeConfiguration<Sticker>
{
    public void Configure(EntityTypeBuilder<Sticker> builder)
    {
        builder.ToTable("stickers");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Name).HasMaxLength(100).IsRequired();
        builder.Property(x => x.Url).HasMaxLength(500).IsRequired();

        builder.HasOne(x => x.StickerSet)
            .WithMany(x => x.Stickers)
            .HasForeignKey(x => x.StickerSetId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(x => x.StickerSetId);
    }
}
