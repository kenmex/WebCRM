using System.Text;

namespace WebCRM.Core;

/// <summary>Builds <c>tel:</c> and <c>mailto:</c> links: one tap to call or write on a phone.</summary>
public static class Links
{
    /// <summary>
    /// "tel:" plus the digits of the number and a leading "+" if there is one: "+30 210 123-4567" gives
    /// "tel:+302101234567". Spaces, brackets, dots and dashes are dropped. Null when there is no digit.
    /// </summary>
    public static string? TelUri(string? phone)
    {
        if (string.IsNullOrWhiteSpace(phone))
        {
            return null;
        }

        var digits = new StringBuilder();
        foreach (var ch in phone.Trim())
        {
            if (char.IsAsciiDigit(ch) || (ch == '+' && digits.Length == 0))
            {
                digits.Append(ch);
            }
        }

        return digits.ToString().Any(char.IsAsciiDigit) ? "tel:" + digits : null;
    }

    /// <summary>"mailto:" plus the (trimmed) address, or null when blank.</summary>
    public static string? MailtoUri(string? email) =>
        string.IsNullOrWhiteSpace(email) ? null : "mailto:" + email.Trim();
}
