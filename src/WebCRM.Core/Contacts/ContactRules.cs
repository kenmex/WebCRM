using System.ComponentModel.DataAnnotations;
using System.Net.Mail;

namespace WebCRM.Core.Contacts;

/// <summary>Contact email rule (docs/data-dictionary.md, Contact.Email): stored trimmed and lower case, valid format.</summary>
public static class ContactRules
{
    public const int MaxEmailLength = 254;

    public const string InvalidEmailMessage = "Enter a valid email address, e.g. name@example.com.";

    /// <summary>Trimmed and lower case; blank gives null.</summary>
    public static string? NormalizeEmail(string? input)
    {
        var value = input?.Trim();
        return string.IsNullOrEmpty(value) ? null : value.ToLowerInvariant();
    }

    /// <summary>
    /// One "@", something before it, a domain with a dot that does not start or end with one, no spaces, at most
    /// 254 characters, and nothing a mail client would read as "Name &lt;address&gt;". Checks a normalised value.
    /// </summary>
    public static bool IsValidEmail(string? normalized)
    {
        if (string.IsNullOrEmpty(normalized) || normalized.Length > MaxEmailLength || normalized.Any(char.IsWhiteSpace))
        {
            return false;
        }

        var at = normalized.IndexOf('@');
        if (at <= 0 || at != normalized.LastIndexOf('@'))
        {
            return false;
        }

        var domain = normalized[(at + 1)..];
        if (!domain.Contains('.') || domain.StartsWith('.') || domain.EndsWith('.') || domain.Contains(".."))
        {
            return false;
        }

        return MailAddress.TryCreate(normalized, out var parsed) && parsed.Address == normalized;
    }
}

/// <summary>Validates an email after normalising it. Empty is valid.</summary>
[AttributeUsage(AttributeTargets.Property)]
public sealed class ContactEmailAttribute : ValidationAttribute
{
    public ContactEmailAttribute() : base(ContactRules.InvalidEmailMessage)
    {
    }

    public override bool IsValid(object? value) =>
        ContactRules.NormalizeEmail(value as string) is not { } email || ContactRules.IsValidEmail(email);
}
