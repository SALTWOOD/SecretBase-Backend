using backend.Tables;
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
        var roleClaim = context.User.FindFirst(ClaimTypes.Role);

        if (roleClaim == null || !int.TryParse(roleClaim.Value, out var userRoleInt)) return Task.CompletedTask;

        var userRole = (UserRole)userRoleInt;
        if (userRole >= requirement.MinimumRole) context.Succeed(requirement);

        return Task.CompletedTask;
    }
}