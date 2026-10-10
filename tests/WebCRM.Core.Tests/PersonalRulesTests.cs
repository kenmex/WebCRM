using Shouldly;
using WebCRM.Core.Personal;

namespace WebCRM.Core.Tests;

public class PersonalRulesTests
{
    [Theory]
    [InlineData("scope=all&q=acme", "q=acme&scope=all")]
    [InlineData("?scope=all&page=3&q=acme", "q=acme&scope=all")]
    [InlineData("Scope=Mine&PAGE=2", "scope=Mine")]
    [InlineData("", "")]
    [InlineData("?", "")]
    [InlineData("page=4", "")]
    [InlineData("b=2&a=2&a=1", "a=1&a=2&b=2")]
    public void Query_strings_are_stored_without_the_page_and_in_a_fixed_order(string input, string expected)
    {
        PersonalRules.NormalizeQueryString(input).ShouldBe(expected);
    }

    [Theory]
    [InlineData("https://evil.example/x?a=1")]
    [InlineData("a=1#frag")]
    [InlineData("a=1 b=2")]
    [InlineData("a=1\nb=2")]
    [InlineData("=1")]
    [InlineData("a/b=1")]
    public void Query_strings_that_are_not_plain_key_value_pairs_are_refused(string input)
    {
        PersonalRules.NormalizeQueryString(input).ShouldBeNull();
    }

    [Fact]
    public void A_query_string_over_the_limit_is_refused()
    {
        PersonalRules.NormalizeQueryString("q=" + new string('x', PersonalRules.MaxQueryStringLength)).ShouldBeNull();
    }

    [Theory]
    [InlineData("  My view ", "My view")]
    [InlineData("", null)]
    [InlineData("   ", null)]
    [InlineData(null, null)]
    public void View_names_are_trimmed_and_must_not_be_empty(string? input, string? expected)
    {
        PersonalRules.NormalizeViewName(input).ShouldBe(expected);
    }

    [Fact]
    public void A_view_name_over_the_limit_is_refused()
    {
        PersonalRules.NormalizeViewName(new string('n', PersonalRules.MaxViewNameLength + 1)).ShouldBeNull();
        PersonalRules.NormalizeViewName(new string('n', PersonalRules.MaxViewNameLength)).ShouldNotBeNull();
    }

    [Theory]
    [InlineData("accounts", true)]
    [InlineData("contacts", true)]
    [InlineData("Accounts", false)]
    [InlineData("users", false)]
    [InlineData(null, false)]
    public void Only_known_lists_can_have_views(string? key, bool expected)
    {
        PersonalRules.IsListKey(key).ShouldBe(expected);
    }
}
