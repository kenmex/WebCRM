using Shouldly;
using WebCRM.Core.Leads;
using WebCRM.Core.Opportunities;

namespace WebCRM.Core.Tests.Leads;

public class LeadRulesTests
{
    [Theory]
    [InlineData("anna@acme.gr", "acme.gr")]
    [InlineData("Anna@ACME.gr", "acme.gr")]
    [InlineData("a@b@c.com", "c.com")]
    public void EmailDomain_is_the_part_after_the_at_sign(string email, string expected) =>
        LeadRules.EmailDomain(email).ShouldBe(expected);

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("no-at-sign")]
    [InlineData("@nolocal.com")]
    [InlineData("nodomain@")]
    public void EmailDomain_is_null_without_a_usable_address(string? email) => LeadRules.EmailDomain(email).ShouldBeNull();

    [Theory]
    [InlineData("anna@gmail.com")]
    [InlineData("anna@Hotmail.com")]
    [InlineData("anna@otenet.gr")]
    public void MatchingDomain_skips_free_mailbox_providers(string email) => LeadRules.MatchingDomain(email).ShouldBeNull();

    [Fact]
    public void MatchingDomain_keeps_a_company_domain() => LeadRules.MatchingDomain("anna@acme.gr").ShouldBe("acme.gr");

    [Theory]
    [InlineData("Maria Papadopoulou", "Maria", "Papadopoulou")]
    [InlineData("  Jan   van der Berg ", "Jan", "van der Berg")]
    [InlineData("Cher", null, "Cher")]
    [InlineData("", null, "")]
    [InlineData(null, null, "")]
    public void SplitName_takes_the_first_word_as_first_name(string? name, string? first, string last) =>
        LeadRules.SplitName(name).ShouldBe((first, last));

    [Theory]
    [InlineData("Acme", "Anna", "Acme deal")]
    [InlineData("  Acme SA ", "Anna", "Acme SA deal")]
    [InlineData(null, "Anna Smith", "Anna Smith deal")]
    [InlineData("  ", "Anna Smith", "Anna Smith deal")]
    public void DefaultOpportunityName_uses_the_company_or_else_the_lead_name(string? company, string name, string expected) =>
        LeadRules.DefaultOpportunityName(company, name).ShouldBe(expected);

    [Fact]
    public void Close_date_is_thirty_days_after_today_in_Athens()
    {
        // 22:30 UTC on 30 June is already 1 July in Athens (UTC+3 in summer).
        var now = new DateTimeOffset(2026, 6, 30, 22, 30, 0, TimeSpan.Zero);

        OpportunityDefaults.CloseDate(now).ShouldBe(new DateOnly(2026, 7, 31));
    }
}
