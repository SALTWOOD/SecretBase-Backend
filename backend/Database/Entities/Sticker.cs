using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace backend.Database.Entities;

[Table("stickers")]
public class Sticker
{
    [Key]
    public int Id { get; set; }

    [Required]
    [MaxLength(100)]
    [Column("name")]
    public string Name { get; set; } = string.Empty;

    [Required]
    [Column("url")]
    public string Url { get; set; } = string.Empty;

    [Column("sticker_set_id")]
    public int StickerSetId { get; set; }

    [ForeignKey(nameof(StickerSetId))]
    public StickerSet? StickerSet { get; set; }
}