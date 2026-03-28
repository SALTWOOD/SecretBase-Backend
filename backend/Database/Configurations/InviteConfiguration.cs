using backend.Database.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace backend.Database.Configurations;

public class InviteConfiguration : IEntityTypeConfiguration<Invite>
{
    public void Configure(EntityTypeBuilder<Invite> builder)
    {
        builder.ToTable("invites");

        builder.HasKey(x => x.Id);

        builder.HasIndex(x => x.Code)
            .IsUnique()
            .HasDatabaseName("unique_invites_code");

        builder.Property(x => x.Code)
            .IsRequired()
            .HasMaxLength(50)
            .HasComment("Unique invite code");

        builder.Property(x => x.CreatedAt)
            .HasColumnType("timestamptz")
            .HasDefaultValueSql("CURRENT_TIMESTAMP");

        builder.Property(x => x.ExpireAt)
            .HasColumnType("timestamptz");

        builder.Property(x => x.MaxUses).HasDefaultValue(1);
        builder.Property(x => x.UsedCount).HasDefaultValue(0);
        builder.Property(x => x.IsDisabled).HasDefaultValue(false);

        // Configure Creator bidirectional relationship
        builder.HasOne(x => x.Creator)
            .WithMany(u => u.MyIssuedInvites)
            .HasForeignKey(x => x.CreatorId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(x => x.UsedBy)
            .WithOne()
            .HasForeignKey(x => x.UsedInviteId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}