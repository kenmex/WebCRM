namespace WebCRM.Core.Interfaces;

/// <summary>The signed-in user for the current operation (circuit or request).</summary>
public interface ICurrentUser
{
    /// <summary>AspNetUsers.Id, or null when nobody is signed in.</summary>
    ValueTask<string?> GetUserIdAsync(CancellationToken cancellationToken = default);
}
