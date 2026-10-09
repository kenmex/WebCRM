using Shouldly;
using WebCRM.Core.Querying;
using WebCRM.Web.Components.Shared;

namespace WebCRM.Web.Tests.Shared;

public class ListStateTests
{
    private static readonly ListScope[] Scopes = [ListScope.Mine, ListScope.Team, ListScope.All];
    private static readonly string[] Keys = ["status", "city"];

    private static ListState Parse(string query, ListScope defaultScope = ListScope.Mine, ListScope[]? scopes = null) =>
        ListState.FromUri("https://localhost/accounts" + query, scopes ?? Scopes, defaultScope, Keys);

    [Fact]
    public void An_empty_query_gives_the_defaults()
    {
        var state = Parse("", ListScope.Team);

        state.Scope.ShouldBe(ListScope.Team);
        state.Page.ShouldBe(1);
        state.Search.ShouldBeNull();
        state.Sort.ShouldBeNull();
        state.Descending.ShouldBeFalse();
        state.Filters.ShouldBeEmpty();
        state.HasFilters.ShouldBeFalse();
    }

    [Fact]
    public void Everything_in_the_query_string_is_read()
    {
        var state = Parse("?scope=all&q=acme&page=3&sort=city&desc=1&status=2&city=Athens&unknown=x");

        state.Scope.ShouldBe(ListScope.All);
        state.Search.ShouldBe("acme");
        state.Page.ShouldBe(3);
        state.Sort.ShouldBe("city");
        state.Descending.ShouldBeTrue();
        state.GetInt("status").ShouldBe(2);
        state.GetFilter("city").ShouldBe("Athens");
        state.Filters.ShouldNotContainKey("unknown");
        state.HasFilters.ShouldBeTrue();
    }

    [Theory]
    [InlineData("?page=0")]
    [InlineData("?page=-2")]
    [InlineData("?page=abc")]
    public void An_invalid_page_falls_back_to_the_first(string query) => Parse(query).Page.ShouldBe(1);

    [Fact]
    public void A_scope_the_user_may_not_use_falls_back_to_the_default()
    {
        // A Sales user opening a shared "?scope=team" link.
        var state = Parse("?scope=team", ListScope.Mine, [ListScope.Mine, ListScope.All]);

        state.Scope.ShouldBe(ListScope.Mine);
    }

    [Fact]
    public void An_unknown_scope_falls_back_to_the_default() => Parse("?scope=everything").Scope.ShouldBe(ListScope.Mine);

    [Fact]
    public void Only_values_that_differ_from_the_defaults_reach_the_url()
    {
        var parameters = new ListState { Scope = ListScope.Mine }.ToQueryParameters(ListScope.Mine, Keys);

        parameters.Values.ShouldAllBe(v => v == null);
    }

    [Fact]
    public void The_state_survives_a_round_trip_through_the_url()
    {
        var original = new ListState
        {
            Scope = ListScope.All,
            Search = "acme & co",
            Page = 4,
            Sort = "name",
            Descending = true,
        };
        original.SetFilter("status", "2");
        original.SetFilter("city", "Athens");

        var parameters = original.ToQueryParameters(ListScope.Mine, Keys);
        var query = "?" + string.Join("&", parameters
            .Where(p => p.Value is not null)
            .Select(p => $"{p.Key}={Uri.EscapeDataString(Convert.ToString(p.Value, System.Globalization.CultureInfo.InvariantCulture)!)}"));
        var restored = Parse(query);

        restored.Scope.ShouldBe(original.Scope);
        restored.Search.ShouldBe(original.Search);
        restored.Page.ShouldBe(original.Page);
        restored.Sort.ShouldBe(original.Sort);
        restored.Descending.ShouldBe(original.Descending);
        restored.GetFilter("status").ShouldBe("2");
        restored.GetFilter("city").ShouldBe("Athens");
    }

    [Fact]
    public void Clearing_a_filter_removes_it_and_blank_values_are_not_kept()
    {
        var state = new ListState();
        state.SetFilter("status", "2");
        state.SetFilter("status", " ");

        state.Filters.ShouldBeEmpty();
    }

    [Fact]
    public void Clone_is_independent_of_the_original()
    {
        var original = new ListState { Page = 2 };
        original.SetFilter("status", "1");

        var copy = original.Clone().With(s =>
        {
            s.Page = 5;
            s.SetFilter("status", null);
        });

        original.Page.ShouldBe(2);
        original.GetFilter("status").ShouldBe("1");
        copy.Page.ShouldBe(5);
        copy.Filters.ShouldBeEmpty();
    }
}
