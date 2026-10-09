using System.ComponentModel.DataAnnotations;

namespace WebCRM.Core.Accounts;

/// <summary>Valid absolute http or https URL. Empty is valid (combine with Required if needed).</summary>
[AttributeUsage(AttributeTargets.Property)]
public sealed class AbsoluteUrlAttribute : ValidationAttribute
{
    public AbsoluteUrlAttribute() : base("Enter a full web address, e.g. https://example.com.")
    {
    }

    public override bool IsValid(object? value) =>
        value is null or ""
        || (value is string s
            && Uri.TryCreate(s, UriKind.Absolute, out var uri)
            && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps));
}
