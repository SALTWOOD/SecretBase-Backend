using backend.Database.Entities;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;

namespace backend.Filters;

public class MinimumRoleRequirement : IAuthorizationRequirement
{
    public UserRole MinimumRole { get; }
    public MinimumRoleRequirement(UserRole minimumRole) => MinimumRole = minimumRole;
}

public class MinimumRoleHandler : AuthorizationHandler<MinimumRoleRequirement>
{
    protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, MinimumRoleRequirement requirement)
    {
        var roleValue = context.User.FindFirstValue(ClaimTypes.Role);

        if (string.IsNullOrEmpty(roleValue)) return Task.CompletedTask;

        if (Enum.TryParse<UserRole>(roleValue, out var role))
        {
            if (role >= requirement.MinimumRole) context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }
}