namespace WebCRM.Core.Leads;

/// <summary>Small rules of lead conversion that need no database (docs/page-spec.md, P13).</summary>
public static class LeadRules
{
    /// <summary>
    /// Shared mailbox providers. Everybody at gmail.com is not the same company, so these domains are never used
    /// to suggest an account.
    /// </summary>
    private static readonly HashSet<string> FreeMailDomains = new(StringComparer.OrdinalIgnoreCase)
    {
        "gmail.com", "googlemail.com", "yahoo.com", "yahoo.gr", "hotmail.com", "hotmail.gr", "outlook.com", "live.com",
        "msn.com", "icloud.com", "me.com", "aol.com", "proton.me", "protonmail.com", "otenet.gr", "mail.com",
    };

    /// <summary>The part after the "@" of a normalised email, or null when there is none.</summary>
    public static string? EmailDomain(string? email)
    {
        var at = email?.LastIndexOf('@') ?? -1;
        return at > 0 && at < email!.Length - 1 ? email[(at + 1)..].Trim().ToLowerInvariant() : null;
    }

    public static bool IsFreeMailDomain(string domain) => FreeMailDomains.Contains(domain);

    /// <summary>The domain to match accounts by: the lead's email domain unless it is a free mailbox provider.</summary>
    public static string? MatchingDomain(string? email) =>
        EmailDomain(email) is { } domain && !IsFreeMailDomain(domain) ? domain : null;

    /// <summary>
    /// The lead's single Name field split for the new contact: the first word is the first name, the rest the last
    /// name ("Jan van der Berg" gives "Jan" and "van der Berg"). One word is a last name. The user can edit both.
    /// </summary>
    public static (string? FirstName, string LastName) SplitName(string? name)
    {
        var parts = (name ?? string.Empty).Split(' ', 2, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return parts.Length switch
        {
            0 => (null, string.Empty),
            1 => (null, parts[0]),
            _ => (parts[0], parts[1]),
        };
    }

    /// <summary>"{Company} deal", or "{Name} deal" for a lead without a company.</summary>
    public static string DefaultOpportunityName(string? company, string name) =>
        $"{(string.IsNullOrWhiteSpace(company) ? name : company).Trim()} deal";
}
