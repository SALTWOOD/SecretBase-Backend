using backend.Database.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace backend.Database.Configurations;

public class CommentConfiguration : IEntityTypeConfiguration<Comment>
{
    public void Configure(EntityTypeBuilder<Comment> builder)
    {
        builder.ToTable("comments");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Content).HasMaxLength(2000).IsRequired();
        builder.Property(x => x.ArticleId).IsRequired();

        builder.Property(x => x.AuthorId).IsRequired(false);

        builder.Property(x => x.ReviewStatus)
            .HasConversion<int>();

        builder.OwnsOne(x => x.Metadata, owned =>
        {
            owned.ToJson();
        });

        builder.Property(x => x.CreatedAt)
            .HasColumnType("timestamptz")
            .HasDefaultValueSql("CURRENT_TIMESTAMP");

        builder.Property(x => x.UpdatedAt)
            .HasColumnType("timestamptz")
            .HasDefaultValueSql("CURRENT_TIMESTAMP");

        builder.HasOne(x => x.Article)
            .WithMany(x => x.Comments)
            .HasForeignKey(x => x.ArticleId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.Author)
            .WithMany()
            .HasForeignKey(x => x.AuthorId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(x => x.ParentComment)
            .WithMany(x => x.Replies)
            .HasForeignKey(x => x.ParentCommentId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => x.ArticleId);
        builder.HasIndex(x => x.AuthorId);
        builder.HasIndex(x => x.ParentCommentId);
        builder.HasIndex(x => x.CreatedAt);
        builder.HasIndex(x => x.IsDeleted);
        builder.HasIndex(x => x.ReviewStatus);
    }
}