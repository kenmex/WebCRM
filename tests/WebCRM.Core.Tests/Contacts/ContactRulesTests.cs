using System.ComponentModel.DataAnnotations;
using Shouldly;
using WebCRM.Core.Contacts;

namespace WebCRM.Core.Tests.Contacts;

public class ContactRulesTests
{
    [Theory]
    [InlineData("  Anna.Smith@Example.COM ", "anna.smith@example.com")]
    [InlineData("a@b.co", "a@b.co")]
    public void NormalizeEmail_trims_and_lowercases(string input, string expected) =>
        ContactRules.NormalizeEmail(input).ShouldBe(expected);

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void NormalizeEmail_gives_null_for_blank_input(string? input) => ContactRules.NormalizeEmail(input).ShouldBeNull();

    [Theory]
    [InlineData("a@b.co")]
    [InlineData("anna.smith@example.com")]
    [InlineData("anna+sales@mail.example.co.uk")]
    [InlineData("first_last-1@sub.domain.org")]
    [InlineData("1234@5678.gr")]
    public void A_well_formed_address_is_valid(string email) => ContactRules.IsValidEmail(email).ShouldBeTrue();

    [Theory]
    [InlineData("")]
    [InlineData("plainaddress")]
    [InlineData("a@b")] // no dot in the domain
    [InlineData("@example.com")]
    [InlineData("anna@")]
    [InlineData("anna@@example.com")]
    [InlineData("anna@exa@mple.com")]
    [InlineData("anna@example.")]
    [InlineData("anna@.example.com")]
    [InlineData("anna@example..com")]
    [InlineData("an na@example.com")]
    [InlineData("anna@exam ple.com")]
    [InlineData("Anna <anna@example.com>")] // display-name form
    [InlineData("\"Anna\" anna@example.com")]
    public void A_malformed_address_is_invalid(string email) => ContactRules.IsValidEmail(email).ShouldBeFalse();

    [Fact]
    public void An_address_over_254_characters_is_invalid()
    {
        var local = new string('a', 64);
        var tooLong = $"{local}@{new string('b', 190)}.com";
        tooLong.Length.ShouldBeGreaterThan(254);

        ContactRules.IsValidEmail(tooLong).ShouldBeFalse();
    }

    [Theory]
    [InlineData("  Anna@Example.com ", true)] // normalised first
    [InlineData("", true)]
    [InlineData(null, true)]
    [InlineData("not an email", false)]
    [InlineData("a@b", false)]
    public void The_attribute_validates_the_normalised_value(string? email, bool valid)
    {
        var model = new ContactEditModel { LastName = "Smith", AccountId = 1, OwnerId = "u", Email = email };
        var results = new List<ValidationResult>();

        Validator.TryValidateObject(model, new ValidationContext(model), results, validateAllProperties: true)
            .ShouldBe(valid);

        if (!valid)
        {
            results.Single().ErrorMessage.ShouldBe(ContactRules.InvalidEmailMessage);
        }
    }

    [Fact]
    public void A_contact_needs_a_last_name_an_account_and_an_owner()
    {
        var model = new ContactEditModel { LastName = "" };
        var results = new List<ValidationResult>();

        Validator.TryValidateObject(model, new ValidationContext(model), results, validateAllProperties: true).ShouldBeFalse();

        results.SelectMany(r => r.MemberNames).ShouldBe(
            [nameof(ContactEditModel.LastName), nameof(ContactEditModel.AccountId), nameof(ContactEditModel.OwnerId)],
            ignoreOrder: true);
    }
}
