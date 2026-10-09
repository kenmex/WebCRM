namespace WebCRM.Core.Users;

/// <summary>The signed-in user's <see cref="UserContext"/> for the current circuit or request.</summary>
public interface IUserContextProvider
{
    /// <summary>Null when nobody is signed in.</summary>
    ValueTask<UserContext?> GetAsync(CancellationToken cancellationToken = default);
}
