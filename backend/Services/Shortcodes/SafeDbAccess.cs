using backend.Database;

namespace backend.Services.Shortcodes;

/// <summary>
/// 安全的数据库访问
/// </summary>
public class SafeDbAccess
{
    private readonly AppDbContext _db;
    private readonly string _shortcodeName;
    private readonly ILogger _logger;

    public SafeDbAccess(AppDbContext db, string shortcodeName, ILogger logger)
    {
        _db = db;
        _shortcodeName = shortcodeName;
        _logger = logger;
    }

    // 提供只读查询能力
    // 注意：这里简化实现，实际应该限制可访问的表和操作
    public async Task<object?> FindByIdAsync(string entityType, int id)
    {
        _logger.LogDebug("[{Shortcode}] FindByIdAsync: {EntityType} {Id}", _shortcodeName, entityType, id);
        return entityType.ToLower() switch
        {
            "user" => await _db.Users.FindAsync(id),
            "article" => await _db.Articles.FindAsync(id),
            "comment" => await _db.Comments.FindAsync(id),
            _ => null
        };
    }
}