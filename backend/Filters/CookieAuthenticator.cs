namespace backend.Filters;

using backend.Database;
using backend.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System.Security.Claims;
using System.Text.Encodings.Web;

public class CookieAuthenticator : AuthenticationHandler<AuthenticationSchemeOptions>
{
    private readonly SessionService _sessionService;
    private readonly AppDbContext _db;

    public CookieAuthenticator(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        SessionService sessionService,
        AppDbContext db) : base(options, logger, encoder)
    {
        _sessionService = sessionService;
        _db = db;
    }

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Cookies.TryGetValue("auth_token", out var token))
            return AuthenticateResult.Fail("Missing token");

        var principal = await _sessionService.ValidateSessionAsync(token);
        if (principal == null)
            return AuthenticateResult.Fail("Session expired or invalid");

        var idClaim = principal.FindFirst(ClaimTypes.NameIdentifier);
        if (idClaim == null || !int.TryParse(idClaim.Value, out var userId))
            return AuthenticateResult.Fail("Invalid session data");

        bool isBanned = await _db.Users
            .AsNoTracking()
            .Where(u => u.Id == userId)
            .Select(u => u.IsBanned)
            .FirstOrDefaultAsync();

        if (isBanned)
        {
            await _sessionService.RemoveSessionAsync(token);
            return AuthenticateResult.Fail("User is banned");
        }

        return AuthenticateResult.Success(new AuthenticationTicket(principal, Scheme.Name));
    }
}