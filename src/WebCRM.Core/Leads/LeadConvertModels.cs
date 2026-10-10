namespace WebCRM.Core.Leads;

[Flags]
public enum AccountMatchReason
{
    CompanyName = 1,
    EmailDomain = 2,
}

/// <summary>An existing account the lead may belong to, offered in the Convert dialog.</summary>
public sealed record AccountMatch(int AccountId, string Name, AccountMatchReason Reasons);

/// <summary>What the Convert dialog starts with: the lead's data split into the records it will create.</summary>
public sealed record ConvertPrefill(
    int LeadId,
    string AccountName,
    string? ContactFirstName,
    string ContactLastName,
    string? ContactEmail,
    string? ContactPhone,
    string OpportunityName,
    IReadOnlyList<AccountMatch> AccountMatches);

/// <summary>
/// The choices made in the Convert dialog. An existing account/contact id means "link"; without one a new record is
/// created from the other fields. A new account always gets a new contact.
/// </summary>
public sealed class LeadConvertRequest
{
    public int LeadId { get; set; }

    public int? ExistingAccountId { get; set; }

    public string? NewAccountName { get; set; }

    /// <summary>A contact of <see cref="ExistingAccountId"/>.</summary>
    public int? ExistingContactId { get; set; }

    public string? ContactFirstName { get; set; }

    public string? ContactLastName { get; set; }

    public string? ContactJobTitle { get; set; }

    public string? ContactEmail { get; set; }

    public string? ContactPhone { get; set; }

    public bool CreateOpportunity { get; set; }

    public string? OpportunityName { get; set; }
}

public enum ConvertStatus
{
    Converted,

    /// <summary>Field errors in <see cref="ConvertResult.FieldErrors"/>; nothing was created.</summary>
    Invalid,

    /// <summary>The lead is already Converted (possibly a moment ago, by someone else). Nothing was created.</summary>
    AlreadyConverted,

    /// <summary>The lead is Disqualified. Nothing was created.</summary>
    Disqualified,

    /// <summary>The lead was changed while converting. Nothing was created; try again.</summary>
    Conflict,

    /// <summary>Missing, deleted or not visible to this user.</summary>
    NotFound,
}

public sealed record ConvertResult(
    ConvertStatus Status,
    int? AccountId = null,
    int? ContactId = null,
    int? OpportunityId = null,
    IReadOnlyDictionary<string, string>? FieldErrors = null);
