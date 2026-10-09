using WebCRM.Core.Entities;
using WebCRM.Core.Interfaces;

namespace WebCRM.Data.Seeding;

/// <summary>Current user for the seeder: always the built-in system account.</summary>
internal sealed class SystemCurrentUser : ICurrentUser
{
    public ValueTask<string?> GetUserIdAsync(CancellationToken cancellationToken = default) =>
        ValueTask.FromResult<string?>(SystemUser.Id);
}
