using System.Globalization;

namespace WebCRM.DemoData;

/// <summary>How much demo data to make. The defaults are the sizes agreed for the demo database.</summary>
public sealed record DemoOptions
{
    public int Accounts { get; init; } = 10_000;

    public int Contacts { get; init; } = 50_000;

    public int Leads { get; init; } = 4_000;

    public int Opportunities { get; init; } = 15_000;

    public int Activities { get; init; } = 200_000;

    /// <summary>Same seed and same <see cref="Today"/> give the same data.</summary>
    public int Seed { get; init; } = 20_261_009;

    /// <summary>"Now" for dates: tasks are overdue, deals are closing soon, relative to this day.</summary>
    public DateOnly Today { get; init; } = DateOnly.FromDateTime(DateTime.UtcNow);

    /// <summary>The same shape at another size, e.g. 0.01 for a quick test run.</summary>
    public DemoOptions Scaled(double factor) => this with
    {
        Accounts = Math.Max(1, (int)(Accounts * factor)),
        Contacts = Math.Max(1, (int)(Contacts * factor)),
        Leads = Math.Max(1, (int)(Leads * factor)),
        Opportunities = Math.Max(1, (int)(Opportunities * factor)),
        Activities = Math.Max(1, (int)(Activities * factor)),
    };

    /// <summary>Reads <c>--accounts 5000 --contacts ... --seed 7 --today 2026-10-09 --scale 0.1</c>. Unknown options are an error.</summary>
    public static DemoOptions Parse(IReadOnlyList<string> args)
    {
        var options = new DemoOptions();
        double? scale = null;

        for (var i = 0; i < args.Count; i++)
        {
            var name = args[i];
            if (!name.StartsWith("--", StringComparison.Ordinal))
            {
                throw new ArgumentException($"Unexpected argument '{name}'.");
            }

            if (i + 1 >= args.Count)
            {
                throw new ArgumentException($"Option {name} needs a value.");
            }

            var value = args[++i];
            switch (name.ToLowerInvariant())
            {
                case "--accounts": options = options with { Accounts = Count(name, value) }; break;
                case "--contacts": options = options with { Contacts = Count(name, value) }; break;
                case "--leads": options = options with { Leads = Count(name, value) }; break;
                case "--opportunities": options = options with { Opportunities = Count(name, value) }; break;
                case "--activities": options = options with { Activities = Count(name, value) }; break;
                case "--seed": options = options with { Seed = int.Parse(value, CultureInfo.InvariantCulture) }; break;
                case "--today":
                    options = options with { Today = DateOnly.ParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture) };
                    break;
                case "--scale":
                    scale = double.Parse(value, CultureInfo.InvariantCulture);
                    if (scale <= 0)
                    {
                        throw new ArgumentException("--scale must be greater than 0.");
                    }

                    break;
                default:
                    throw new ArgumentException($"Unknown option '{name}'.");
            }
        }

        return scale is { } factor ? options.Scaled(factor) : options;
    }

    private static int Count(string name, string value) =>
        int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var count) && count >= 0
            ? count
            : throw new ArgumentException($"{name} needs a whole number, not '{value}'.");
}
