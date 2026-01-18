using backend.Tables;
using SqlSugar;

namespace backend.Services;

public class UserService(JwtService jwt, ISqlSugarClient db)
{
    public async Task UpdateLastLoginAsync(UserTable user, HttpContext context)
    {
        var lastLogin = new LastLogin
        {
            Time = DateTime.UtcNow,
            IpAddress = context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            UserAgent = context.Request.Headers.UserAgent.ToString()
        };

        await db.Updateable<UserTable>()
                .SetColumns(u => u.LastLoginInfo == lastLogin)
                .Where(u => u.Id == user.Id)
                .ExecuteCommandAsync();
    }
}