using System.ComponentModel.DataAnnotations;
using System.Text;

namespace WebCRM.Core.Accounts;

/// <summary>
/// "VAT / Tax ID" rule (docs/data-dictionary.md, Account.VatNumber). Works for clients in any country:
/// everything is normalised (upper case, no spaces, dots or dashes) and must be 4 to 20 letters or digits;
/// only numbers starting with EL (Greece) are checked strictly: 9 digits and the ΑΦΜ check digit.
/// </summary>
public static class VatNumberRules
{
    public const int MinLength = 4;

    public const int MaxLength = 20;

    public const string GreekPrefix = "EL";

    public const string GreekCountryCode = "GR";

    public const string InvalidMessage =
        "Enter a VAT / Tax ID of 4 to 20 letters or digits. Spaces, dots and dashes are ignored.";

    public const string InvalidGreekMessage =
        "Greek VAT numbers (EL) have exactly 9 digits, e.g. EL094259216.";

    public const string InvalidGreekCheckDigitMessage =
        "This Greek VAT number (ΑΦΜ) is not valid: its check digit does not match. Please check the number.";

    // "ΕΛ" typed on a Greek keyboard: capital Greek Epsilon and Lambda, which look like E and L.
    private const string GreekLettersPrefix = "ΕΛ";

    /// <summary>
    /// Upper case, strips whitespace, dots and dashes, and turns a Greek-letter "ΕΛ" prefix into "EL". A bare
    /// 9-digit number becomes an EL number only when <paramref name="defaultCountryCode"/> is GR: elsewhere it is
    /// stored as typed (a US EIN has 9 digits too). Blank input gives null. Does not check the format; see
    /// <see cref="Validate"/>.
    /// </summary>
    public static string? Normalize(string? input, string? defaultCountryCode = null)
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

        var greekCompany = string.Equals(defaultCountryCode, GreekCountryCode, StringComparison.OrdinalIgnoreCase);
        return greekCompany && value.Length == 9 && value.All(char.IsAsciiDigit) ? GreekPrefix + value : value;
    }

    /// <summary>
    /// Check digit of a Greek ΑΦΜ (9 digits). The first 8 digits are weighted 256, 128, ... 2; the sum modulo 11,
    /// modulo 10, must equal the 9th digit. 000000000 passes the arithmetic but is not a real number.
    /// </summary>
    public static bool IsValidGreekAfm(string nineDigits)
    {
        if (nineDigits.Length != 9 || !nineDigits.All(char.IsAsciiDigit) || nineDigits == "000000000")
        {
            return false;
        }

        var sum = 0;
        var weight = 256;
        for (var i = 0; i < 8; i++)
        {
            sum += (nineDigits[i] - '0') * weight;
            weight /= 2;
        }

        return sum % 11 % 10 == nineDigits[8] - '0';
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
            if (normalized.Length != 11 || !normalized.Skip(2).All(char.IsAsciiDigit))
            {
                return InvalidGreekMessage;
            }

            return IsValidGreekAfm(normalized[2..]) ? null : InvalidGreekCheckDigitMessage;
        }

        var valid = normalized.Length is >= MinLength and <= MaxLength
            && normalized.All(c => char.IsAsciiLetterUpper(c) || char.IsAsciiDigit(c));

        return valid ? null : InvalidMessage;
    }
}

/// <summary>
/// Validates a VAT / Tax ID after normalising it. Empty is valid. The form cannot know the company's default
/// country, so a bare 9-digit number passes here; the service normalises with the country and validates again.
/// </summary>
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
