using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using WebCRM.Core.Entities;
using WebCRM.Core.Lookups;
using WebCRM.Core.Users;
using WebCRM.Web.Components.Shared;
using WebCRM.Web.Tests.TestSupport;

namespace WebCRM.Web.Tests.Shared;

public class PickerTests : MudTestContext
{
    private readonly FakeLookupService _lookups = new(
        new LookupOption(1, "Prospect", true),
        new LookupOption(2, "Active", true),
        new LookupOption(3, "Retired", false));

    private readonly FakeOwnerService _owners = new(
        new OwnerOption("u1", "Alice", true),
        new OwnerOption("u2", "Bob", true),
        new OwnerOption("u3", "Gone Gary", false));

    public PickerTests()
    {
        Services.AddSingleton<ILookupService>(_lookups);
        Services.AddSingleton<IOwnerService>(_owners);
        Services.AddSingleton<IUserContextProvider>(new FakeUserContextProvider());
        StartProviders();
    }

    // ---- LookupSelect ----

    [Fact]
    public void LookupSelect_asks_for_its_kind_and_passes_the_current_value_so_an_inactive_one_still_shows()
    {
        var cut = Render<LookupSelect>(p => p
            .Add(x => x.Kind, LookupKind.AccountStatus)
            .Add(x => x.Label, "Status")
            .Add(x => x.Value, 3));

        _lookups.Calls.ShouldBe([(LookupKind.AccountStatus, (int?)3)]);
        cut.WaitForAssertion(() => cut.Find("input").GetAttribute("value").ShouldBe("Retired (inactive)"));
    }

    [Fact]
    public void LookupSelect_shows_the_name_of_an_active_value()
    {
        var cut = Render<LookupSelect>(p => p
            .Add(x => x.Kind, LookupKind.AccountStatus)
            .Add(x => x.Value, 2));

        cut.WaitForAssertion(() => cut.Find("input").GetAttribute("value").ShouldBe("Active"));
    }

    [Fact]
    public void LookupSelect_loads_once_per_kind()
    {
        var cut = Render<LookupSelect>(p => p.Add(x => x.Kind, LookupKind.Industry).Add(x => x.Value, 1));

        cut.Render(p => p.Add(x => x.Kind, LookupKind.Industry).Add(x => x.Value, 2));

        _lookups.Calls.Count.ShouldBe(1);
    }

    // ---- OwnerPicker ----

    [Fact]
    public void OwnerPicker_asks_for_the_owners_the_current_user_may_assign()
    {
        Render<OwnerPicker>(p => p.Add(x => x.Value, "u1"));

        _owners.Calls.ShouldBe([(FakeUserContextProvider.Sales, (string?)"u1")]);
    }

    [Fact]
    public void OwnerPicker_shows_the_current_owner_and_marks_an_inactive_one()
    {
        var active = Render<OwnerPicker>(p => p.Add(x => x.Value, "u2"));
        active.WaitForAssertion(() => active.Find("input").GetAttribute("value").ShouldBe("Bob"));

        var inactive = Render<OwnerPicker>(p => p.Add(x => x.Value, "u3"));
        inactive.WaitForAssertion(() => inactive.Find("input").GetAttribute("value").ShouldBe("Gone Gary (inactive)"));
    }

    // ---- OwnerBadge ----

    [Theory]
    [InlineData("Maria Papadopoulou", "MP")]
    [InlineData("Kostas", "K")]
    [InlineData("  ", "?")]
    public void OwnerBadge_shows_initials(string name, string initials)
    {
        var cut = Render<OwnerBadge>(p => p.Add(x => x.Name, name));

        cut.Find(".mud-avatar").TextContent.Trim().ShouldBe(initials);
    }

    [Fact]
    public void OwnerBadge_marks_an_inactive_owner_and_copes_with_a_missing_name()
    {
        Render<OwnerBadge>(p => p.Add(x => x.Name, "Maria").Add(x => x.IsActive, false))
            .Markup.ShouldContain("Maria (inactive)");

        Render<OwnerBadge>().Markup.ShouldContain("Unknown");
    }
}
