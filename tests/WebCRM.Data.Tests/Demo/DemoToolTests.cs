using Shouldly;
using WebCRM.DemoData;

namespace WebCRM.Data.Tests.Demo;

public class DemoToolTests
{
    private const string Local = "Server=localhost;Database=WebCRM;Trusted_Connection=True;TrustServerCertificate=True";

    // ---- The guard ----

    [Fact]
    public void It_runs_in_Development_against_a_local_server()
    {
        DemoGuard.Refusal("Development", Local).ShouldBeNull();
        DemoGuard.Refusal("development", Local).ShouldBeNull();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("Production")]
    [InlineData("Staging")]
    public void It_refuses_any_environment_but_Development(string? environment)
    {
        var refusal = DemoGuard.Refusal(environment, Local);

        refusal.ShouldNotBeNull();
        refusal.ShouldContain("Development");
    }

    [Theory]
    [InlineData("Server=db.monsterasp.net;Database=crm;User Id=u;Password=p")]
    [InlineData("Server=tcp:myserver.database.windows.net,1433;Database=crm")]
    [InlineData("Data Source=10.0.0.5;Initial Catalog=crm")]
    [InlineData("Server=notlocalhost;Database=crm")]
    public void It_refuses_a_server_that_is_not_this_machine_even_in_Development(string connectionString)
    {
        var refusal = DemoGuard.Refusal("Development", connectionString);

        refusal.ShouldNotBeNull();
        refusal.ShouldContain("not on this machine");
    }

    [Theory]
    [InlineData("Server=localhost;Database=x")]
    [InlineData("Server=.;Database=x")]
    [InlineData("Server=(local);Database=x")]
    [InlineData("Server=127.0.0.1;Database=x")]
    [InlineData("Server=tcp:localhost,1433;Database=x")]
    [InlineData(@"Server=.\SQLEXPRESS;Database=x")]
    [InlineData(@"Server=localhost\MSSQLSERVER01;Database=x")]
    [InlineData("Server=(localdb)\\MSSQLLocalDB;Database=x")]
    public void These_spellings_all_mean_the_local_machine(string connectionString) =>
        DemoGuard.Refusal("Development", connectionString).ShouldBeNull();

    [Fact]
    public void This_machines_own_name_counts_as_local()
    {
        DemoGuard.Refusal("Development", $"Server={Environment.MachineName};Database=x").ShouldBeNull();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("this is not a connection string")]
    public void It_refuses_a_missing_or_broken_connection_string(string? connectionString) =>
        DemoGuard.Refusal("Development", connectionString).ShouldNotBeNull();

    // ---- Options ----

    [Fact]
    public void The_defaults_are_the_agreed_sizes()
    {
        var options = new DemoOptions();

        (options.Accounts, options.Contacts, options.Leads, options.Opportunities, options.Activities)
            .ShouldBe((10_000, 50_000, 4_000, 15_000, 200_000));
    }

    [Fact]
    public void Options_are_read_from_the_command_line()
    {
        var options = DemoOptions.Parse(["--accounts", "500", "--contacts", "2000", "--seed", "7", "--today", "2026-01-31"]);

        options.Accounts.ShouldBe(500);
        options.Contacts.ShouldBe(2000);
        options.Seed.ShouldBe(7);
        options.Today.ShouldBe(new DateOnly(2026, 1, 31));
        options.Activities.ShouldBe(200_000);
    }

    [Fact]
    public void Scale_shrinks_everything_by_the_same_factor_but_never_to_nothing()
    {
        var options = DemoOptions.Parse(["--scale", "0.01"]);

        (options.Accounts, options.Contacts, options.Leads, options.Opportunities, options.Activities)
            .ShouldBe((100, 500, 40, 150, 2_000));
        new DemoOptions().Scaled(0.00001).Accounts.ShouldBe(1);
    }

    [Theory]
    [InlineData("--accounts")]
    [InlineData("--accounts", "ten")]
    [InlineData("--accounts", "-5")]
    [InlineData("--bogus", "1")]
    [InlineData("accounts", "5")]
    [InlineData("--scale", "0")]
    [InlineData("--today", "tomorrow")]
    public void Bad_options_are_an_error_not_a_guess(params string[] args)
    {
        Should.Throw<Exception>(() => DemoOptions.Parse(args)).ShouldBeAssignableTo<Exception>();
    }

    // ---- Password ----

    [Theory]
    [InlineData("Demo-Password-123")]
    [InlineData("aB3$aB3$aB3$")]
    public void A_password_that_meets_the_app_rules_is_accepted(string password) => DemoSeeder.CheckPassword(password).ShouldBeNull();

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("Short1$a")] // under 12
    [InlineData("alllowercase123$")]
    [InlineData("ALLUPPERCASE123$")]
    [InlineData("NoDigitsHere$$$$")]
    [InlineData("NoSymbolsHere1234")]
    public void A_password_that_would_not_pass_the_app_is_refused_before_anything_is_written(string? password) =>
        DemoSeeder.CheckPassword(password).ShouldNotBeNull();

    // ---- Text helpers ----

    [Theory]
    [InlineData("Αθήνα", "αθηνα")]
    [InlineData("ΑΘΗΝΑ", "αθηνα")]
    [InlineData("Ελληνική Εμπορική Α.Ε.", "ελληνικη εμπορικη α.ε.")]
    public void Fold_ignores_accents_and_case_like_the_database_collation(string text, string expected) =>
        Pools.Fold(text).ShouldBe(expected);

    [Theory]
    [InlineData("Παπαδόπουλος", "papadopoylos")]
    [InlineData("Γιώργος", "giorgos")]
    [InlineData("Müller GmbH", "mullergmbh")]
    public void Slug_turns_names_into_plain_latin_for_emails(string text, string expected) => Pools.Slug(text).ShouldBe(expected);
}
