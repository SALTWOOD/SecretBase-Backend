namespace backend.Types.Responses;

public class StickerSetResponse
{
    public required int Id { get; set; }
    public required string Name { get; set; }
    public int StickerCount { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class StickerSetDetailResponse
{
    public required int Id { get; set; }
    public required string Name { get; set; }
    public required List<StickerResponse> Stickers { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class StickerResponse
{
    public required int Id { get; set; }
    public required string Name { get; set; }
}

public readonly record struct StickerImageUrlResponse(string Url);

public readonly record struct PresignedStickerUrl(string Key, string Url, DateTime ExpiresAt);

public class UploadedStickerResponse
{
    public required int Id { get; set; }
    public required string Name { get; set; }
    public required string Key { get; set; }
    public required string UploadUrl { get; set; }
    public DateTime ExpiresAt { get; set; }
}
