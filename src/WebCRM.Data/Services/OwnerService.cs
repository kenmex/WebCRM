using Microsoft.EntityFrameworkCore;
using WebCRM.Core.Entities;
using WebCRM.Core.Users;

namespace WebCRM.Data.Services;

public sealed class OwnerService(IDbContextFactory<CrmDbContext> factory) : IOwnerService
{
    public async Task<IReadOnlyList<OwnerOption>> GetAssignableAsync(
        UserContext user, string? currentOwnerId = null, CancellationToken cancellationToken = default)
    {
        await using var db = await factory.CreateDbContextAsync(cancellationToken);

        var users = db.Users.AsNoTracking().Where(u => u.Id != SystemUser.Id);
        var self = user.UserId;
        var teamId = user.TeamId;

        users = user.Role switch
        {
            RoleNames.Admin => users.Where(u => u.IsActive || u.Id == currentOwnerId),
            RoleNames.Manager => users.Where(u =>
                (u.IsActive && (u.Id == self || (teamId != null && u.TeamId == teamId))) || u.Id == currentOwnerId),
            _ => users.Where(u => u.Id == self || u.Id == currentOwnerId),
        };

        return await users
            .OrderBy(u => u.DisplayName)
            .Select(u => new OwnerOption(u.Id, u.DisplayName, u.IsActive))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<OwnerOption>> GetFilterOptionsAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await factory.CreateDbContextAsync(cancellationToken);

        return await db.Users.AsNoTracking()
            .Where(u => u.Id != SystemUser.Id)
            .OrderBy(u => u.DisplayName)
            .Select(u => new OwnerOption(u.Id, u.DisplayName, u.IsActive))
            .ToListAsync(cancellationToken);
    }
}
