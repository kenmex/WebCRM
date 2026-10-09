using WebCRM.Core.Entities;
using WebCRM.Core.Users;

namespace WebCRM.Core.Accounts;

public static class AccountAccess
{
    /// <summary>
    /// The one place that decides which accounts a user may see (lists, search, export, dashboard, API).
    /// Stub until Phase 5: every user sees every record.
    /// </summary>
    public static IQueryable<Account> VisibleTo(this IQueryable<Account> accounts, UserContext user) => accounts;
}
