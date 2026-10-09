using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using WebCRM.Core.Entities;

namespace WebCRM.Web.Services;

/// <summary>
/// Builds the claims written into the sign-in cookie. Adds the user's DisplayName so the UI
/// can show it without a database round trip. Existing cookies get the claim at the next sign-in.
/// </summary>
public sealed class AppUserClaimsPrincipalFactory(
    UserManager<User> userManager,
    RoleManager<IdentityRole> roleManager,
    IOptions<IdentityOptions> options)
    : UserClaimsPrincipalFactory<User, IdentityRole>(userManager, roleManager, options)
{
    public const string DisplayNameClaimType = "display_name";

    protected override async Task<ClaimsIdentity> GenerateClaimsAsync(User user)
    {
        var identity = await base.GenerateClaimsAsync(user);

        if (!string.IsNullOrWhiteSpace(user.DisplayName))
        {
            identity.AddClaim(new Claim(DisplayNameClaimType, user.DisplayName));
        }

        return identity;
    }
}
