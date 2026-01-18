namespace backend;

using backend.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;
using System.Text.Encodings.Web;

public class CookieAuthenticator : AuthenticationHandler<AuthenticationSchemeOptions>
{
    JwtService _jwt;

    public CookieAuthenticator(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        JwtService jwt) : base(options, logger, encoder)
    {
        _jwt = jwt;
    }

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Cookies.TryGetValue("auth_token", out var token)) return AuthenticateResult.Fail("Missing auth_token cookie");

        var principal = await _jwt.ValidateToken(token);
        if (principal == null) return AuthenticateResult.Fail("Invalid token");

        var ticket = new AuthenticationTicket(principal, Scheme.Name);

        return AuthenticateResult.Success(ticket);
    }
}