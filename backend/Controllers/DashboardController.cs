using backend.Database.Entities;
using backend.Services;
using backend.Types.Response;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace backend.Controllers;

[Route("dashboard")]
public class DashboardController(BaseServices deps) : BaseApiController(deps)
{
    [HttpGet("stats")]
    [Authorize]
    [ProducesResponseType(typeof(DashboardStatsResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetStats()
    {
        const int takes = 15;
        var totalUsers = await _db.Users.CountAsync();
        var totalArticles = await _db.Articles.CountAsync();
        var totalComments = await _db.Comments.CountAsync();

        var recentArticles = await _db.Articles
            .Where(a => a.IsPublished)
            .OrderByDescending(a => a.CreatedAt)
            .Take(takes)
            .Select(a => new RecentActivityItem
            {
                Type = "article_published",
                Title = a.Title,
                Author = a.Author != null ? a.Author.Username : null,
                Time = a.CreatedAt
            })
            .ToListAsync();

        var recentComments = await _db.Comments
            .Where(c => !c.IsDeleted && c.ReviewStatus == ReviewStatus.Approved)
            .OrderByDescending(c => c.CreatedAt)
            .Take(takes)
            .Select(c => new RecentActivityItem
            {
                Type = "comment_posted",
                Title = c.Content.Length > 80 ? c.Content.Substring(0, 80) + "..." : c.Content,
                Author = c.Author != null ? c.Author.Username : c.Metadata.GuestNickname,
                Time = c.CreatedAt
            })
            .ToListAsync();

        var activities = recentArticles
            .Concat(recentComments)
            .OrderByDescending(a => a.Time)
            .Take(takes)
            .ToList();

        return Ok(new DashboardStatsResponse
        {
            TotalUsers = totalUsers,
            TotalArticles = totalArticles,
            TotalComments = totalComments,
            RecentActivities = activities
        });
    }
}
