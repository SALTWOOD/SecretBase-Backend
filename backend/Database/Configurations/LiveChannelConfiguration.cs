using backend.Database.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace backend.Database.Configurations;

public class LiveChannelConfiguration : IEntityTypeConfiguration<LiveChannel>
{
    public void Configure(EntityTypeBuilder<LiveChannel> builder)
    {
        builder.ToTable("live_channels");

        builder.HasKey(x => x.OwnerUserId);
        builder.HasIndex(x => x.IsLive);
        builder.HasIndex(x => x.IsEnabled);

        builder.Property(x => x.Title)
            .IsRequired()
            .HasMaxLength(80);

        builder.Property(x => x.CoverUrl)
            .HasMaxLength(1024);

        builder.Property(x => x.StreamKeyHash)
            .IsRequired()
            .HasMaxLength(128);

        builder.HasOne(x => x.OwnerUser)
            .WithMany()
            .HasForeignKey(x => x.OwnerUserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
