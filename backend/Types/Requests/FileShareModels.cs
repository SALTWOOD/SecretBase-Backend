using System.ComponentModel.DataAnnotations;
using backend.Database.Entities;

namespace backend.Types.Requests;

public class FileShareCreateRequest
{
    public required string Bucket { get; set; } = string.Empty;

    public required string Key { get; set; } = string.Empty;

    public required string FileName { get; set; } = string.Empty;

    public bool IsPublic { get; set; } = true;

    public DateTime? ExpiresAt { get; set; }
}

public class FileShareUpdateRequest
{
    public string? Key { get; set; }

    public string? Bucket { get; set; }

    public bool? IsEnabled { get; set; }

    public bool? IsPublic { get; set; }

    public DateTime? ExpiresAt { get; set; }

    public string? FileName { get; set; }
}