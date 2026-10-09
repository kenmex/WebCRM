using System.Globalization;
using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;
using WebCRM.Core.Entities;
using WebCRM.Core.Users;

namespace WebCRM.Web.Services;

/// <summary>
/// Builds the <see cref="UserContext"/> from the sign-in claims (no database round trip).
/// A role or team change shows after the user signs in again.
/// </summary>
public sealed class UserContextProvider(AuthenticationStateProvider authenticationStateProvider) : IUserContextProvider
{
    public async ValueTask<UserContext?> GetAsync(CancellationToken cancellationToken = default)
    {
        var user = (await authenticationStateProvider.GetAuthenticationStateAsync()).User;
        if (user.FindFirstValue(ClaimTypes.NameIdentifier) is not { } userId)
        {
            return null;
        }

        // Highest role wins; a user with no role is treated as Sales (least privilege).
        var role = user.IsInRole(RoleNames.Admin) ? RoleNames.Admin
            : user.IsInRole(RoleNames.Manager) ? RoleNames.Manager
            : RoleNames.Sales;

        int? teamId = int.TryParse(
            user.FindFirstValue(AppUserClaimsPrincipalFactory.TeamIdClaimType),
            NumberStyles.None, CultureInfo.InvariantCulture, out var id) ? id : null;

        return new UserContext(userId, role, teamId);
    }
}
