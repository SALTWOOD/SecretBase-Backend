using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace backend.Database.Entities;

[Table("sticker_sets")]
public class StickerSet
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }

    [Required]
    [MaxLength(100)]
    [Column("name")]
    public string Name { get; set; } = string.Empty;

    public List<Sticker> Stickers { get; set; } = new();

    [Required]
    [Column("created_by")]
    public int CreatorId { get; set; }

    [ForeignKey(nameof(CreatorId))]
    public User? CreatedBy { get; set; }

    [Required]
    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Required]
    [Column("updated_at")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}