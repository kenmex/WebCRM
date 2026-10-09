namespace WebCRM.Core.Records;

/// <summary>How a save of any record ended (accounts, contacts, ...). Shared so every detail page handles it the same way.</summary>
public enum SaveStatus
{
    Saved,

    /// <summary>Field errors in <see cref="SaveResult.FieldErrors"/>; nothing was saved.</summary>
    Invalid,

    /// <summary>
    /// Something worth a second look (a similar name, a duplicate email). Nothing was saved; repeat with
    /// <see cref="SaveOptions.AcceptWarnings"/> to go ahead.
    /// </summary>
    Warning,

    /// <summary>Someone saved the record since it was loaded. Nothing was saved.</summary>
    Conflict,

    /// <summary>Missing, deleted or not visible to this user.</summary>
    NotFound,
}

/// <param name="ChangedBy">Display name of the user who saved the record since it was opened.</param>
/// <param name="ChangedAtUtc">When they saved it.</param>
public sealed record ConcurrencyConflict(string? ChangedBy, DateTime? ChangedAtUtc);

/// <param name="Warnings">The records behind a warning, e.g. the similar account names.</param>
/// <param name="WarningMessage">Headline for the warning, e.g. "Similar accounts already exist".</param>
public sealed record SaveResult(
    SaveStatus Status,
    int Id = 0,
    IReadOnlyDictionary<string, string>? FieldErrors = null,
    IReadOnlyList<string>? Warnings = null,
    string? WarningMessage = null,
    ConcurrencyConflict? Conflict = null);

public sealed record SaveOptions
{
    /// <summary>The user saw the warning and wants to save anyway.</summary>
    public bool AcceptWarnings { get; init; }

    /// <summary>Save over a concurrent change. Honoured for Admin only.</summary>
    public bool Overwrite { get; init; }
}
