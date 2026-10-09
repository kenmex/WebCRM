using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;
using WebCRM.Core.Interfaces;

namespace WebCRM.Web.Services;

/// <summary>
/// Current user from the Blazor authentication state. Works on interactive pages (per circuit)
/// and on static server-rendered pages (per request).
/// </summary>
public sealed class CurrentUser(AuthenticationStateProvider authenticationStateProvider) : ICurrentUser
{
    public async ValueTask<string?> GetUserIdAsync(CancellationToken cancellationToken = default)
    {
        var state = await authenticationStateProvider.GetAuthenticationStateAsync();
        return state.User.FindFirstValue(ClaimTypes.NameIdentifier);
    }
}
