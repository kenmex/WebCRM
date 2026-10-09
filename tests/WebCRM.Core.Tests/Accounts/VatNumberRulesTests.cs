using System.ComponentModel.DataAnnotations;
using Shouldly;
using WebCRM.Core.Accounts;

namespace WebCRM.Core.Tests.Accounts;

public class VatNumberRulesTests
{
    [Theory]
    [InlineData("EL123456789", "EL123456789")]
    [InlineData("el123456789", "EL123456789")]
    [InlineData("el 123.456-789", "EL123456789")]
    [InlineData("  de 123 456 789  ", "DE123456789")]
    [InlineData("NL 1234.56.789 B01", "NL123456789B01")]
    [InlineData("EL–123456789", "EL123456789")] // en dash
    public void Normalize_uppercases_and_strips_spaces_dots_and_dashes(string input, string expected) =>
        VatNumberRules.Normalize(input).ShouldBe(expected);

    [Theory]
    [InlineData("123456789")]
    [InlineData("123 456 789")]
    [InlineData("123.456.789")]
    [InlineData("123-456-789")]
    public void Normalize_turns_a_bare_9_digit_Greek_AFM_into_an_EL_number(string input) =>
        VatNumberRules.Normalize(input).ShouldBe("EL123456789");

    [Theory]
    [InlineData("ΕΛ123456789")] // Greek capital Epsilon Lambda
    [InlineData("ελ 123 456 789")] // Greek lower case, with spaces
    public void Normalize_accepts_the_prefix_typed_with_Greek_letters(string input) =>
        VatNumberRules.Normalize(input).ShouldBe("EL123456789");

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(" . - ")]
    public void Normalize_gives_null_for_blank_input(string? input) => VatNumberRules.Normalize(input).ShouldBeNull();

    [Fact]
    public void Normalize_leaves_other_bare_digit_strings_alone_so_they_are_rejected_later()
    {
        VatNumberRules.Normalize("12345678").ShouldBe("12345678");
        VatNumberRules.Normalize("1234567890").ShouldBe("1234567890");
    }

    [Theory]
    [InlineData("EL123456789")]
    [InlineData("DE123456789")]
    [InlineData("FR12345678901")]
    [InlineData("BE0123456789")]
    [InlineData("ATU12345678")]
    [InlineData("NL123456789B01")] // 2 + 12
    [InlineData("CY12")] // 2 + 2, the shortest
    public void Validate_accepts_a_country_prefix_plus_2_to_12_alphanumerics(string value) =>
        VatNumberRules.Validate(value).ShouldBeNull();

    [Fact]
    public void Validate_accepts_empty() => VatNumberRules.Validate(null).ShouldBeNull();

    [Theory]
    [InlineData("EL12345678")] // 8 digits
    [InlineData("EL1234567890")] // 10 digits
    [InlineData("ELABCDEFGHI")] // letters
    [InlineData("EL12345678A")]
    public void Validate_requires_exactly_9_digits_for_Greece(string value) =>
        VatNumberRules.Validate(value).ShouldBe(VatNumberRules.InvalidGreekMessage);

    [Theory]
    [InlineData("12345678")] // no country prefix
    [InlineData("1234567890")]
    [InlineData("D123456789")] // one-letter prefix
    [InlineData("DE1")] // too short
    [InlineData("DE1234567890123")] // 13 after the prefix
    [InlineData("DE12 34")] // not normalised
    [InlineData("DE12_34")]
    [InlineData("ΔΕ123456")] // Greek letters, not Latin
    public void Validate_rejects_everything_else_with_a_clear_message(string value) =>
        VatNumberRules.Validate(value).ShouldBe(VatNumberRules.InvalidMessage);

    [Theory]
    [InlineData("el 123.456-789", true)]
    [InlineData("123456789", true)]
    [InlineData("", true)]
    [InlineData("12345", false)]
    [InlineData("EL12345", false)]
    public void The_attribute_validates_the_normalised_value(string value, bool valid)
    {
        var model = new AccountEditModel { Name = "x", AccountStatusId = 1, OwnerId = "u", VatNumber = value };
        var results = new List<ValidationResult>();

        Validator.TryValidateObject(model, new ValidationContext(model), results, validateAllProperties: true)
            .ShouldBe(valid);

        if (!valid)
        {
            results.Single().MemberNames.ShouldBe([nameof(AccountEditModel.VatNumber)]);
        }
    }
}
