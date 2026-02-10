using backend.Database.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace backend.Database.Configurations;

public class UserCredentialConfiguration : IEntityTypeConfiguration<UserCredential>
{
    public void Configure(EntityTypeBuilder<UserCredential> builder)
    {
        builder.ToTable("user_credentials");

        builder.HasKey(x => x.Id);

        builder.HasIndex(x => x.CredentialId)
               .IsUnique()
               .HasDatabaseName("unique_user_credentials_credentialid");

        builder.Property(x => x.CredentialId)
               .HasColumnType("bytea")
               .IsRequired();

        builder.Property(x => x.PublicKey)
               .HasColumnType("bytea")
               .IsRequired();

        builder.Property(x => x.SignatureCounter)
               .HasColumnType("bigint")
               .IsRequired();

        builder.Property(x => x.Nickname)
               .HasMaxLength(100);

        builder.Property(x => x.CreatedAt)
               .HasColumnType("timestamptz")
               .HasDefaultValueSql("CURRENT_TIMESTAMP");

        builder.HasOne<User>()
               .WithMany()
               .HasForeignKey(x => x.UserId)
               .OnDelete(DeleteBehavior.Cascade);
    }
}