using System.ComponentModel.DataAnnotations;

namespace backend.Types.Requests;

public class CreateStickerSetRequest
{
    [Required]
    [MaxLength(100)]
    public required string Name { get; set; }
}

public class UpdateStickerSetRequest
{
    [MaxLength(100)]
    public string? Name { get; set; }
}

public class UploadStickersRequest
{
    [Required]
    public required List<StickerUploadItem> Items { get; set; }
}

public class StickerUploadItem
{
    [Required]
    [MaxLength(100)]
    public required string Name { get; set; }

    public string? ContentType { get; set; }
}
