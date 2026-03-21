using backend.Services;
using Microsoft.AspNetCore.Mvc;
using StackExchange.Redis;
using System.Net.Mime;
using backend.Database.Models;

namespace backend.Controllers;

[ApiController]
[Route("[controller]")]
[Produces(MediaTypeNames.Application.Json)]
public class BaseApiController(BaseServices deps) : ControllerBase
{
    protected readonly Supabase.Client _supa = deps.Supa;
    protected readonly IDatabase _redis = deps.Redis.GetDatabase();

    protected string JwtToken => Request.Cookies[ApplicationConstants.AUTH_TOKEN_COOKIE_NAME].ThrowIfNull();
    protected async Task<Supabase.Gotrue.User?> GetCurrentUserAsync() => await _supa.Auth.GetUser(JwtToken);

    protected async Task<Guid> GetCurrentUserIdAsync() 
    {
        var user = await _supa.Auth.GetUser(JwtToken);
        return Guid.Parse(user.ThrowIfNull().Id!);
    }

    protected async Task<Profile?> GetCurrentProfileAsync()
    {
        var id = await GetCurrentUserIdAsync();
        return await _supa.From<Profile>().Where(i => i.Id == id).Single();
    }
}
