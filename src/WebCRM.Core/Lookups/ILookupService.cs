namespace WebCRM.Core.Lookups;

public enum LookupKind
{
    Industry,
    AccountStatus,
    LeadSource,
    LeadStatus,
    ActivityType,
    LostReason,
    Salutation,
}

public sealed record LookupOption(int Id, string Name, bool IsActive, string? SystemCode = null);

public interface ILookupService
{
    /// <summary>
    /// Active values in dropdown order. <paramref name="currentId"/> is included even when inactive,
    /// so a record keeps showing a value that was deactivated after it was saved.
    /// </summary>
    Task<IReadOnlyList<LookupOption>> GetOptionsAsync(
        LookupKind kind, int? currentId = null, CancellationToken cancellationToken = default);
}
