using backend.Database.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace backend.Database.Configurations;

public class SettingConfiguration : IEntityTypeConfiguration<Setting>
{
    public void Configure(EntityTypeBuilder<Setting> builder)
    {
        builder.ToTable("settings");

        builder.HasKey(x => x.Key);

        builder.Property(x => x.Key)
            .HasMaxLength(255)
            .IsRequired();

        builder.Property(x => x.Value)
            .HasColumnType("text");

        builder.Property(x => x.Type)
            .HasConversion<int>();
    }
}