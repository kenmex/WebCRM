using Microsoft.Extensions.Configuration;
using WebCRM.DemoData;

// Demo data tool. See docs/adr/0004-demo-data.md.
//   dotnet run --project src/WebCRM.DemoData -- seed [--accounts N --contacts N --leads N --opportunities N --activities N]
//                                                    [--scale 0.1] [--seed N] [--today yyyy-MM-dd]
//   dotnet run --project src/WebCRM.DemoData -- wipe       remove the demo data only
//   dotnet run --project src/WebCRM.DemoData -- measure    time the list pages and search against the 300 ms / 1 s budgets
//
// Needs (dotnet user-secrets, shared with WebCRM.Web):
//   ConnectionStrings:DefaultConnection   already set for the web app
//   Demo:Password                         the password of every demo user (12+ characters, upper, lower, digit, symbol)

const string Usage = """
    Usage: dotnet run --project src/WebCRM.DemoData -- <command> [options]
      seed      wipe the previous demo data and make a new set (options: --accounts --contacts --leads --opportunities
                --activities --scale --seed --today)
      wipe      remove the demo data only
      measure   time the Accounts and Contacts lists and global search on the demo data
    """;

Console.OutputEncoding = System.Text.Encoding.UTF8;

var command = args.FirstOrDefault()?.ToLowerInvariant();
if (command is null or "help" or "--help" or "-h")
{
    Console.WriteLine(Usage);
    return 0;
}

var environment = Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT") ?? Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");
var configuration = new ConfigurationBuilder()
    .AddUserSecrets(typeof(DemoGuard).Assembly)
    .AddEnvironmentVariables()
    .Build();
var connectionString = configuration["ConnectionStrings:DefaultConnection"];

if (DemoGuard.Refusal(environment, connectionString) is { } refusal)
{
    Console.Error.WriteLine(refusal);
    return 2;
}

try
{
    var seeder = new DemoSeeder(connectionString!);
    switch (command)
    {
        case "seed":
            {
                var options = DemoOptions.Parse(args.Skip(1).ToList());
                var password = configuration["Demo:Password"];
                if (string.IsNullOrEmpty(password))
                {
                    Console.Error.WriteLine(
                        "Demo:Password is not set. Set it once with:" + Environment.NewLine
                        + "  dotnet user-secrets set \"Demo:Password\" \"<12+ characters, upper, lower, digit, symbol>\" --project src/WebCRM.DemoData");
                    return 1;
                }

                var report = await seeder.SeedAsync(options, password, Console.WriteLine);
                Console.WriteLine();
                Console.WriteLine($"Done. Generated in {report.Generating.TotalSeconds:N1} s, written in {report.Writing.TotalSeconds:N1} s.");
                Console.WriteLine(
                    $"  {report.Accounts:N0} accounts, {report.Addresses:N0} addresses, {report.Contacts:N0} contacts, "
                    + $"{report.Opportunities:N0} opportunities, {report.Leads:N0} leads, {report.Activities:N0} activities, "
                    + $"{report.Users} users, {report.Teams} teams.");
                Console.WriteLine($"Demo users (password: Demo:Password), e.g. admin@{DemoDataGenerator.UserDomain}, "
                    + $"manager-athens@..., sales-athens-1@... Sign in with the email address.");
                return 0;
            }

        case "wipe":
            await seeder.WipeAsync(Console.WriteLine);
            return 0;

        case "measure":
            {
                var measureOptions = ParseMeasure(args.Skip(1).ToList());
                Console.WriteLine($"Measuring ({DemoMeasure.Warmups} warm-up runs, then {measureOptions.Runs ?? DemoMeasure.Runs} timed runs each; budgets {DemoMeasure.ListBudgetMs:N0} ms lists, {DemoMeasure.SearchBudgetMs:N0} ms search, on p95):");
                var measurements = await new DemoMeasure(connectionString!).RunAsync(Console.WriteLine, measureOptions);
                Console.WriteLine();
                Console.WriteLine(DemoMeasure.ToMarkdown(measurements));
                var slow = measurements.Count(m => !m.WithinBudget);
                Console.WriteLine();
                Console.WriteLine(slow == 0 ? "Everything is within its budget." : $"{slow} scenario(s) are over budget.");
                return slow == 0 ? 0 : 3;
            }

        default:
            Console.Error.WriteLine($"Unknown command '{command}'." + Environment.NewLine + Usage);
            return 1;
    }
}
catch (Exception ex) when (ex is DemoException or ArgumentException)
{
    Console.Error.WriteLine(ex.Message);
    return 1;
}

static MeasureOptions ParseMeasure(IReadOnlyList<string> args)
{
    var options = new MeasureOptions();
    for (var i = 0; i < args.Count; i++)
    {
        switch (args[i].ToLowerInvariant())
        {
            case "--only" when i + 1 < args.Count: options = options with { Only = args[++i] }; break;
            case "--runs" when i + 1 < args.Count: options = options with { Runs = int.Parse(args[++i]) }; break;
            case "--sql": options = options with { ShowSql = true }; break;
            default: throw new ArgumentException($"Unknown measure option '{args[i]}'. Use --only <text>, --runs <n>, --sql.");
        }
    }

    return options;
}
