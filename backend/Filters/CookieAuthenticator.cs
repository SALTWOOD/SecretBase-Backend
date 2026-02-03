namespace backend.Filters;

using backend.Services;
using backend.Tables;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;
using SqlSugar;
using System.Text.Encodings.Web;

public class CookieAuthenticator : AuthenticationHandler<AuthenticationSchemeOptions>
{
    JwtService _jwt;
    ISqlSugarClient _db;

    public CookieAuthenticator(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        JwtService jwt,
        ISqlSugarClient db) : base(options, logger, encoder)
    {
        _jwt = jwt;
        _db = db;
    }

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Cookies.TryGetValue("auth_token", out var token)) return AuthenticateResult.Fail("Missing auth_token cookie");

        var principal = await _jwt.ValidateToken(token);
        if (principal == null) return AuthenticateResult.Fail("Invalid token");

        var idClaim = principal.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier);
        if (idClaim == null || string.IsNullOrEmpty(idClaim.Value))
        {
            return AuthenticateResult.Fail("Missing user id claim");
        }
        int userId;
        if (!int.TryParse(idClaim.Value, out userId))
        {
            return AuthenticateResult.Fail("Invalid user id claim");
        }

        bool isBanned = await _db.Queryable<UserTable>()
            .Where(u => u.Id == userId)
            .Select(u => u.IsBanned)
            .FirstAsync();

        if (isBanned) return AuthenticateResult.Fail("User is banned");

        var ticket = new AuthenticationTicket(principal, Scheme.Name);

        return AuthenticateResult.Success(ticket);
    }
}