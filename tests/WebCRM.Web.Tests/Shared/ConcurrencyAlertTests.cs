using WebCRM.Core.Records;
using Bunit;
using Shouldly;
using WebCRM.Core.Accounts;
using WebCRM.Web.Components.Shared;
using WebCRM.Web.Tests.TestSupport;

namespace WebCRM.Web.Tests.Shared;

public class ConcurrencyAlertTests : MudTestContext
{
    private static readonly ConcurrencyConflict Conflict =
        new("Maria", new DateTime(2026, 10, 9, 12, 0, 0, DateTimeKind.Utc));

    [Fact]
    public void Nothing_is_shown_without_a_conflict()
    {
        var cut = Render<ConcurrencyAlert>();

        cut.Markup.Trim().ShouldBeEmpty();
    }

    [Fact]
    public void Names_who_changed_the_record_and_when_in_Athens_time()
    {
        var cut = Render<ConcurrencyAlert>(p => p.Add(x => x.Conflict, Conflict));

        // 12:00 UTC is 15:00 in Athens in October (daylight saving time).
        cut.Markup.ShouldContain("Changed by Maria");
        cut.Markup.ShouldContain("9 Oct 2026 15:00");
    }

    [Fact]
    public void Falls_back_when_the_other_user_is_unknown()
    {
        var cut = Render<ConcurrencyAlert>(p => p.Add(x => x.Conflict, new ConcurrencyConflict(null, null)));

        cut.Markup.ShouldContain("Changed by someone else");
    }

    [Fact]
    public void Overwrite_is_offered_to_admins_only()
    {
        Render<ConcurrencyAlert>(p => p.Add(x => x.Conflict, Conflict))
            .FindAll("button").Select(b => b.TextContent.Trim()).ShouldNotContain(t => t.StartsWith("Overwrite"));

        Render<ConcurrencyAlert>(p => p.Add(x => x.Conflict, Conflict).Add(x => x.CanOverwrite, true))
            .FindAll("button").Select(b => b.TextContent.Trim()).ShouldContain(t => t.StartsWith("Overwrite"));
    }

    [Fact]
    public void Reload_and_Overwrite_raise_their_callbacks()
    {
        var reloads = 0;
        var overwrites = 0;
        var cut = Render<ConcurrencyAlert>(p => p
            .Add(x => x.Conflict, Conflict)
            .Add(x => x.CanOverwrite, true)
            .Add(x => x.OnReload, () => reloads++)
            .Add(x => x.OnOverwrite, () => overwrites++));

        cut.FindAll("button").Single(b => b.TextContent.Contains("Reload")).Click();
        cut.FindAll("button").Single(b => b.TextContent.Contains("Overwrite")).Click();

        reloads.ShouldBe(1);
        overwrites.ShouldBe(1);
    }
}
