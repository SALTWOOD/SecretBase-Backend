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

public class PresignStickerUploadRequest
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

public class ConfirmStickerUploadRequest
{
    [Required]
    public required List<ConfirmStickerItem> Items { get; set; }
}

public class ConfirmStickerItem
{
    [Required]
    public required string Key { get; set; }

    [Required]
    [MaxLength(100)]
    public required string Name { get; set; }
}
