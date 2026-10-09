using System.ComponentModel.DataAnnotations;
using Shouldly;
using WebCRM.Core.Accounts;

namespace WebCRM.Core.Tests.Accounts;

public class WebsiteRulesTests
{
    [Theory]
    [InlineData("mexdb.com", "https://mexdb.com")]
    [InlineData("  mexdb.com/about  ", "https://mexdb.com/about")]
    [InlineData("www.mexdb.com", "https://www.mexdb.com")]
    [InlineData("//mexdb.com", "https://mexdb.com")]
    [InlineData("http://mexdb.com", "http://mexdb.com")]
    [InlineData("https://mexdb.com", "https://mexdb.com")]
    [InlineData("HTTPS://MEXDB.COM", "HTTPS://MEXDB.COM")]
    public void Normalize_adds_https_only_when_the_scheme_is_missing(string input, string expected) =>
        WebsiteRules.Normalize(input).ShouldBe(expected);

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Normalize_gives_null_for_blank_input(string? input) => WebsiteRules.Normalize(input).ShouldBeNull();

    [Theory]
    [InlineData("mexdb.com")]
    [InlineData("www.mexdb.com/path?x=1")]
    [InlineData("http://mexdb.com")]
    [InlineData("https://mexdb.com:8443/")]
    [InlineData("localhost:5000")]
    public void A_valid_address_passes_after_normalising(string input) =>
        WebsiteRules.IsValid(WebsiteRules.Normalize(input)).ShouldBeTrue();

    [Theory]
    [InlineData("ftp://mexdb.com")] // has a scheme, but not http or https
    [InlineData("not a url")]
    [InlineData("https://not a url")]
    [InlineData("javascript:alert(1)")]
    [InlineData("mailto:someone@mexdb.com")]
    [InlineData("https://user:secret@mexdb.com")]
    [InlineData("https://")]
    [InlineData("http://")]
    public void An_address_that_is_still_not_an_absolute_http_or_https_url_is_rejected(string input) =>
        WebsiteRules.IsValid(WebsiteRules.Normalize(input)).ShouldBeFalse();

    [Theory]
    [InlineData("mexdb.com", true)]
    [InlineData("", true)]
    [InlineData(null, true)]
    [InlineData("ftp://mexdb.com", false)]
    [InlineData("not a url", false)]
    public void The_attribute_accepts_a_missing_scheme_and_rejects_the_rest(string? value, bool valid)
    {
        var model = new AccountEditModel { Name = "x", AccountStatusId = 1, OwnerId = "u", Website = value };
        var results = new List<ValidationResult>();

        Validator.TryValidateObject(model, new ValidationContext(model), results, validateAllProperties: true)
            .ShouldBe(valid);

        if (!valid)
        {
            results.Single().ErrorMessage.ShouldBe(WebsiteRules.InvalidMessage);
        }
    }
}
