using System.ComponentModel.DataAnnotations;

namespace WebCRM.Core.Accounts;

/// <summary>Website rule (docs/data-dictionary.md, Account.Website): a valid absolute http or https URL.</summary>
public static class WebsiteRules
{
    public const string InvalidMessage = "Enter a valid web address, e.g. example.com or https://example.com.";

    /// <summary>
    /// Trims, and adds "https://" when the user left out the scheme ("mexdb.com" becomes "https://mexdb.com").
    /// Blank input gives null. Does not check validity; see <see cref="IsValid"/>.
    /// </summary>
    public static string? Normalize(string? input)
    {
        var value = input?.Trim();
        if (string.IsNullOrEmpty(value))
        {
            return null;
        }

        if (value.StartsWith("//", StringComparison.Ordinal))
        {
            return "https:" + value;
        }

        return value.Contains("://", StringComparison.Ordinal) ? value : "https://" + value;
    }

    /// <summary>True for a normalised value that is an absolute http or https URL with a host and no credentials.</summary>
    public static bool IsValid(string? normalized) =>
        normalized is not null
        && !normalized.Any(char.IsWhiteSpace)
        && Uri.TryCreate(normalized, UriKind.Absolute, out var uri)
        && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps)
        && uri.Host.Length > 0
        && uri.UserInfo.Length == 0;
}

/// <summary>Validates a website after normalising it, so a missing scheme is not an error. Empty is valid.</summary>
[AttributeUsage(AttributeTargets.Property)]
public sealed class WebsiteAttribute : ValidationAttribute
{
    public WebsiteAttribute() : base(WebsiteRules.InvalidMessage)
    {
    }

    public override bool IsValid(object? value) =>
        WebsiteRules.Normalize(value as string) is not { } normalized || WebsiteRules.IsValid(normalized);
}
