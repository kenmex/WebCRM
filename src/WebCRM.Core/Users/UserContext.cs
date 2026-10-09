using WebCRM.Core.Entities;

namespace WebCRM.Core.Users;

/// <summary>
/// Who is asking, for record visibility and ownership rules. Built from the sign-in claims,
/// so a change to the user's role or team shows after the next sign-in.
/// </summary>
/// <param name="UserId">AspNetUsers.Id.</param>
/// <param name="Role">The highest of Admin, Manager, Sales (see RoleNames).</param>
/// <param name="TeamId">The user's team, or null when they have none.</param>
public sealed record UserContext(string UserId, string Role, int? TeamId)
{
    public bool IsAdmin => Role == RoleNames.Admin;

    public bool IsManager => Role == RoleNames.Manager;
}
