using backend.Database.Entities;

namespace backend.Types.Response;

public class DashboardStatsResponse
{
    public int TotalUsers { get; set; }
    public int TotalArticles { get; set; }
    public int TotalComments { get; set; }
    public List<RecentActivityItem> RecentActivities { get; set; } = new();
}

public class RecentActivityItem
{
    public required string Type { get; set; }
    public required string Title { get; set; }
    public string? Author { get; set; }
    public DateTime Time { get; set; }
}
