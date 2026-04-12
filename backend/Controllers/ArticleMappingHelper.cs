using backend.Database.Entities;
using backend.Types.Responses;

namespace backend.Controllers;

public static class ArticleMappingHelper
{
    public static ArticleResponse ToListResponse(Article a)
    {
        return new ArticleResponse
        {
            Id = a.Id,
            Title = a.Title,
            Author = a.Author != null ? UserDto.FromUser(a.Author) : new UserDto(),
            CreatedAt = a.CreatedAt,
            UpdatedAt = a.UpdatedAt,
            IsPublished = a.IsPublished,
            CoverUrl = a.CoverUrl,
            CommentCount = a.Comments.Count,
            Type = a.Type,
            Slug = a.Slug
        };
    }

    public static ArticleResponse ToDetailResponse(Article a)
    {
        return new ArticleResponse
        {
            Id = a.Id,
            Title = a.Title,
            Content = a.Content,
            Author = a.Author != null ? UserDto.FromUser(a.Author) : new UserDto(),
            CreatedAt = a.CreatedAt,
            UpdatedAt = a.UpdatedAt,
            IsPublished = a.IsPublished,
            CoverUrl = a.CoverUrl,
            CommentCount = a.Comments.Count,
            Type = a.Type,
            Slug = a.Slug
        };
    }
}
