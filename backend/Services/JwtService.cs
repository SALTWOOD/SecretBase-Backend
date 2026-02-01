using backend.Tables;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using SqlSugar;
using System.Diagnostics.CodeAnalysis;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace backend.Services;

public class JwtService
{
    private readonly ISqlSugarClient _db;
    private string? _secret;
    private string? _issuer;
    private string? _audience;
    private int? _expireHours;

    public JwtService(ISqlSugarClient db)
    {
        _db = db;
        Refresh().Wait();
    }

    [MemberNotNull(nameof(_secret), nameof(_issuer), nameof(_audience), nameof(_expireHours))]
    private void ThrowIfNotInitialized()
    {
        if (_secret == null || _issuer == null || _audience == null || _expireHours == null)
        {
            throw new InvalidOperationException("JWT settings have not been initialized. Please call Refresh() before using this service.");
        }
    }

    public async Task Refresh()
    {
        var settings = await _db.Queryable<SettingTable>()
            .Where(s => s.Key.StartsWith(SettingKeys.Site.Security.Jwt.Prefix))
            .ToListAsync();
        _secret = settings.First(s => s.Key == SettingKeys.Site.Security.Jwt.Secret).GetValue<string>().ThrowIfNull();
        _issuer = settings.First(s => s.Key == SettingKeys.Site.Security.Jwt.Issuer).GetValue<string>().ThrowIfNull();
        _audience = settings.First(s => s.Key == SettingKeys.Site.Security.Jwt.Audience).GetValue<string>().ThrowIfNull();
        _expireHours = settings.First(s => s.Key == SettingKeys.Site.Security.Jwt.ExpireHours).GetValue<int>();
    }

    public async Task<string> IssueJwtToken(UserTable user)
    {
        ThrowIfNotInitialized();
        var handler = new JsonWebTokenHandler();
        var key = Encoding.UTF8.GetBytes(_secret);

        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(
            [
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new Claim(ClaimTypes.Role, user.Role.ToString())
            ]),
            Expires = DateTime.UtcNow.AddHours(_expireHours.Value),
            Issuer = _issuer,
            Audience = _audience,
            SigningCredentials = new SigningCredentials(
                new SymmetricSecurityKey(key),
                SecurityAlgorithms.HmacSha256Signature)
        };

        return handler.CreateToken(tokenDescriptor);
    }

    public async Task<ClaimsPrincipal?> ValidateToken(string token)
    {
        if (string.IsNullOrEmpty(token)) return null;
        ThrowIfNotInitialized();

        var tokenHandler = new JwtSecurityTokenHandler();
        var key = Encoding.ASCII.GetBytes(_secret);

        try
        {
            var validationParameters = new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(key),
                ValidateIssuer = true,
                ValidIssuer = _issuer,
                ValidateAudience = true,
                ValidAudience = _audience,
                ValidateLifetime = true,
                ClockSkew = TimeSpan.Zero
            };

            var principal = tokenHandler.ValidateToken(token, validationParameters, out SecurityToken validatedToken);
            return await Task.FromResult(principal);
        }
        catch
        {
            return null;
        }
    }
}