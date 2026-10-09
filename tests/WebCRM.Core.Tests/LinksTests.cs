using Shouldly;

namespace WebCRM.Core.Tests;

public class LinksTests
{
    [Theory]
    [InlineData("+30 210 123-4567", "tel:+302101234567")]
    [InlineData("(210) 123.4567", "tel:2101234567")]
    [InlineData("  6944 123 456 ", "tel:6944123456")]
    [InlineData("0030 210 1234567", "tel:00302101234567")]
    [InlineData("+1 (415) 555-0100", "tel:+14155550100")]
    [InlineData("210 123 4567 +", "tel:2101234567")] // a plus is only meaningful at the start
    public void TelUri_keeps_digits_and_a_leading_plus(string phone, string expected) =>
        Links.TelUri(phone).ShouldBe(expected);

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("call me")]
    [InlineData("+")]
    public void TelUri_is_null_without_digits(string? phone) => Links.TelUri(phone).ShouldBeNull();

    [Fact]
    public void MailtoUri_trims_and_prefixes()
    {
        Links.MailtoUri("  anna@example.com ").ShouldBe("mailto:anna@example.com");
        Links.MailtoUri(" ").ShouldBeNull();
        Links.MailtoUri(null).ShouldBeNull();
    }
}
