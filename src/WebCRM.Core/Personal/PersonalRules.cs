using System.Text;

namespace WebCRM.Core.Personal;

/// <summary>Limits and the query-string rule for saved views, favourites and recent views (docs/data-dictionary.md).</summary>
public static class PersonalRules
{
    public const int MaxRecentViews = 50;

    /// <summary>Viewing the same record again this soon after the last time writes nothing.</summary>
    public static readonly TimeSpan RecentViewRefresh = TimeSpan.FromMinutes(1);

    public const int MaxSavedViewsPerList = 30;

    public const int MaxViewNameLength = 100;

    /// <summary>The refusal for a name the user already has on that list; the save dialog offers to replace on seeing it.</summary>
    public const string DuplicateViewNameMessage = "You already have a view with that name.";

    public const int MaxQueryStringLength = 2000;

    public static readonly string[] ListKeys = ["accounts", "contacts", "leads", "opportunities", "activities"];

    public static bool IsListKey(string? listKey) => listKey is not null && ListKeys.Contains(listKey);

    /// <summary>Trimmed, and null when empty or too long.</summary>
    public static string? NormalizeViewName(string? name)
    {
        var trimmed = name?.Trim();
        return string.IsNullOrEmpty(trimmed) || trimmed.Length > MaxViewNameLength ? null : trimmed;
    }

    /// <summary>
    /// The query string of a view in its stored form: no leading "?", no page, parameters in a fixed order, and nothing
    /// but <c>key=value</c> pairs, so it can only ever be appended to the list's own address. Null when it is not acceptable
    /// (a fragment, a full address, control characters, too long). An empty string is fine: it is the list without filters.
    /// </summary>
    public static string? NormalizeQueryString(string? queryString)
    {
        var text = (queryString ?? string.Empty).Trim().TrimStart('?');
        if (text.Length > MaxQueryStringLength || text.Any(char.IsControl) || text.Contains('#') || text.Contains(' '))
        {
            return null;
        }

        var pairs = new List<(string Key, string Value)>();
        foreach (var part in text.Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var cut = part.IndexOf('=');
            var key = cut < 0 ? part : part[..cut];
            var value = cut < 0 ? string.Empty : part[(cut + 1)..];
            if (key.Length == 0 || !key.All(c => char.IsAsciiLetterOrDigit(c) || c is '_' or '-' or '.'))
            {
                return null;
            }

            // The page is where you happen to be in the list, not part of the view.
            if (!key.Equals("page", StringComparison.OrdinalIgnoreCase))
            {
                pairs.Add((key.ToLowerInvariant(), value));
            }
        }

        var builder = new StringBuilder();
        foreach (var (key, value) in pairs.OrderBy(p => p.Key, StringComparer.Ordinal).ThenBy(p => p.Value, StringComparer.Ordinal))
        {
            builder.Append(builder.Length == 0 ? "" : "&").Append(key).Append('=').Append(value);
        }

        return builder.ToString();
    }
}
