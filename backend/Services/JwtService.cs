using backend.Tables;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using SqlSugar;
using System.Security.Claims;
using System.Text;

namespace backend.Services;

public class JwtService
{
    private readonly ISqlSugarClient _db;

    public JwtService(ISqlSugarClient db)
    {
        _db = db;
    }

    public async Task<string> IssueJwtToken(UserTable user)
    {
        var settings = await _db.Queryable<SettingTable>()
            .Where(s => s.Key.StartsWith(SettingKeys.Site.Security.Jwt.Prefix))
            .ToListAsync();

        var secret = settings.First(s => s.Key == SettingKeys.Site.Security.Jwt.Secret).GetValue<string>().ThrowIfNull();
        var issuer = settings.First(s => s.Key == SettingKeys.Site.Security.Jwt.Issuer).GetValue<string>().ThrowIfNull();
        var audience = settings.First(s => s.Key == SettingKeys.Site.Security.Jwt.Audience).GetValue<string>().ThrowIfNull();
        var expireHours = settings.First(s => s.Key == SettingKeys.Site.Security.Jwt.ExpireHours).GetValue<int>();

        var handler = new JsonWebTokenHandler();
        var key = Encoding.UTF8.GetBytes(secret);

        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(
            [
                new Claim("id", user.Id.ToString()),
                new Claim("role", user.Role.ToString())
            ]),
            Expires = DateTime.UtcNow.AddHours(expireHours),
            Issuer = issuer,
            Audience = audience,
            SigningCredentials = new SigningCredentials(
                new SymmetricSecurityKey(key),
                SecurityAlgorithms.HmacSha256Signature)
        };

        return handler.CreateToken(tokenDescriptor);
    }
}