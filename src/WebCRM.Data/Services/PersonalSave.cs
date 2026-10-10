using Microsoft.EntityFrameworkCore;

namespace WebCRM.Data.Services;

internal static class PersonalSave
{
    /// <summary>
    /// Saves changes that remove or touch rows another request may have removed first (two clicks, two tabs).
    /// A row that is already gone is the outcome we wanted anyway, so that is not an error.
    /// Returns false when that happened.
    /// </summary>
    public static async Task<bool> SaveTolerantAsync(CrmDbContext db, CancellationToken cancellationToken)
    {
        try
        {
            await db.SaveChangesAsync(cancellationToken);
            return true;
        }
        catch (DbUpdateConcurrencyException)
        {
            return false;
        }
    }
}
