using WebCRM.Core.Entities;
using WebCRM.Core.Querying;
using WebCRM.Core.Records;
using WebCRM.Core.Users;

namespace WebCRM.Core.Leads;

public static class LeadAccess
{
    /// <summary>
    /// The one place that decides which leads a user may see (lists, search, export, dashboard, API).
    /// Stub until Phase 5: every user sees every record.
    /// </summary>
    public static IQueryable<Lead> VisibleTo(this IQueryable<Lead> leads, UserContext user) => leads;
}

public interface ILeadService
{
    Task<PagedResult<LeadListItem>> SearchAsync(
        LeadQuery query, UserContext user, CancellationToken cancellationToken = default);

    /// <summary>Null when the lead does not exist or the user cannot see it (the same 404 for both).</summary>
    Task<LeadDetail?> GetAsync(int id, UserContext user, CancellationToken cancellationToken = default);

    /// <summary>A blank form: status New, owner the current user.</summary>
    Task<LeadEditModel> NewAsync(UserContext user, CancellationToken cancellationToken = default);

    /// <summary>
    /// Saves a lead. The status can be any except Converted, which only <see cref="ConvertAsync"/> sets; a Converted
    /// lead cannot be saved at all (it is read-only).
    /// </summary>
    Task<SaveResult> SaveAsync(
        LeadEditModel model, UserContext user, SaveOptions? options = null,
        CancellationToken cancellationToken = default);

    /// <summary>Soft delete. False when the lead does not exist, is not visible, or is Converted (never deleted).</summary>
    Task<bool> DeleteAsync(int id, UserContext user, CancellationToken cancellationToken = default);

    /// <summary>Starting values for the Convert dialog. Null when the lead is missing, not visible or cannot be converted.</summary>
    Task<ConvertPrefill?> GetConvertPrefillAsync(int leadId, UserContext user, CancellationToken cancellationToken = default);

    /// <summary>
    /// Converts the lead in one transaction (US3): creates or links the account and contact, optionally creates an
    /// opportunity, moves the lead's activities and notes to the contact, and marks the lead Converted. Either all of
    /// it happens or none of it. New records are owned by the converting user.
    /// </summary>
    Task<ConvertResult> ConvertAsync(
        LeadConvertRequest request, UserContext user, CancellationToken cancellationToken = default);
}
