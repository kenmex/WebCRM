using System.ComponentModel.DataAnnotations;
using System.Text;

namespace WebCRM.Core.Accounts;

/// <summary>
/// VAT number rule (docs/data-dictionary.md, Account.VatNumber). The stored form is normalised:
/// upper case, without spaces, dots or dashes, and always with the 2-letter country prefix.
/// </summary>
public static class VatNumberRules
{
    public const string GreekPrefix = "EL";

    public const string InvalidMessage =
        "Enter a VAT number as a 2-letter country code followed by 2 to 12 letters or digits, e.g. EL123456789 or DE123456789.";

    public const string InvalidGreekMessage =
        "Greek VAT numbers (EL) have exactly 9 digits, e.g. EL123456789 or just 123456789.";

    // "ΕΛ" typed on a Greek keyboard: capital Greek Epsilon and Lambda, which look like E and L.
    private const string GreekLettersPrefix = "ΕΛ";

    /// <summary>
    /// Upper case, strips whitespace, dots and dashes, turns a Greek-letter "ΕΛ" prefix into "EL", and adds
    /// "EL" to a bare 9-digit Greek ΑΦΜ. Blank input gives null. Does not check the format; see <see cref="Validate"/>.
    /// </summary>
    public static string? Normalize(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return null;
        }

        var text = new StringBuilder(input.Length);
        foreach (var ch in input)
        {
            if (char.IsWhiteSpace(ch) || ch is '.' or '-' or '‐' or '‑' or '‒' or '–' or '—')
            {
                continue;
            }

            text.Append(char.ToUpperInvariant(ch));
        }

        var value = text.ToString();
        if (value.Length == 0)
        {
            return null;
        }

        if (value.StartsWith(GreekLettersPrefix, StringComparison.Ordinal))
        {
            value = GreekPrefix + value[GreekLettersPrefix.Length..];
        }

        // A bare 9-digit number is a Greek ΑΦΜ.
        return value.Length == 9 && value.All(char.IsAsciiDigit) ? GreekPrefix + value : value;
    }

    /// <summary>Null when the (already normalised) value is valid or empty, otherwise the message to show.</summary>
    public static string? Validate(string? normalized)
    {
        if (normalized is null)
        {
            return null;
        }

        if (normalized.StartsWith(GreekPrefix, StringComparison.Ordinal))
        {
            return normalized.Length == 11 && normalized.Skip(2).All(char.IsAsciiDigit) ? null : InvalidGreekMessage;
        }

        var valid = normalized.Length is >= 4 and <= 14
            && char.IsAsciiLetterUpper(normalized[0])
            && char.IsAsciiLetterUpper(normalized[1])
            && normalized.Skip(2).All(c => char.IsAsciiLetterUpper(c) || char.IsAsciiDigit(c));

        return valid ? null : InvalidMessage;
    }
}

/// <summary>Validates a VAT number after normalising it. Empty is valid.</summary>
[AttributeUsage(AttributeTargets.Property)]
public sealed class VatNumberAttribute : ValidationAttribute
{
    protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
    {
        var message = VatNumberRules.Validate(VatNumberRules.Normalize(value as string));
        return message is null
            ? ValidationResult.Success
            : new ValidationResult(message, validationContext.MemberName is { } member ? [member] : null);
    }
}
