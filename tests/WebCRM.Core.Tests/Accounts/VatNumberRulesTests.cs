using System.ComponentModel.DataAnnotations;
using Shouldly;
using WebCRM.Core.Accounts;

namespace WebCRM.Core.Tests.Accounts;

public class VatNumberRulesTests
{
    // ---- Normalisation: the same for everyone ----

    [Theory]
    [InlineData("EL094259216", "EL094259216")]
    [InlineData("el094259216", "EL094259216")]
    [InlineData("el 094.259-216", "EL094259216")]
    [InlineData("  de 123 456 789  ", "DE123456789")]
    [InlineData("NL 1234.56.789 B01", "NL123456789B01")]
    [InlineData("EL–094259216", "EL094259216")] // en dash
    [InlineData("12-3456789", "123456789")] // US EIN
    [InlineData("GB 123 4567 89", "GB123456789")] // UK
    [InlineData("gb gd001", "GBGD001")] // UK government department
    [InlineData("CHE-123.456.789 MWST", "CHE123456789MWST")] // Switzerland
    [InlineData("no 123 456 785 mva", "NO123456785MVA")] // Norway
    public void Normalize_uppercases_and_strips_spaces_dots_and_dashes(string input, string expected) =>
        VatNumberRules.Normalize(input).ShouldBe(expected);

    [Theory]
    [InlineData("ΕΛ094259216")] // Greek capital Epsilon Lambda
    [InlineData("ελ 094 259 216")] // Greek lower case, with spaces
    public void Normalize_accepts_the_prefix_typed_with_Greek_letters(string input) =>
        VatNumberRules.Normalize(input).ShouldBe("EL094259216");

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(" . - ")]
    public void Normalize_gives_null_for_blank_input(string? input) => VatNumberRules.Normalize(input).ShouldBeNull();

    // ---- A bare 9-digit number: a Greek ΑΦΜ only for a Greek company ----

    [Theory]
    [InlineData("094259216")]
    [InlineData("094 259 216")]
    [InlineData("094.259.216")]
    [InlineData("094-259-216")]
    public void A_bare_9_digit_number_becomes_an_EL_number_when_the_company_is_in_Greece(string input)
    {
        VatNumberRules.Normalize(input, "GR").ShouldBe("EL094259216");
        VatNumberRules.Normalize(input, "gr").ShouldBe("EL094259216");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("US")]
    [InlineData("DE")]
    [InlineData("")]
    public void A_bare_9_digit_number_is_stored_as_typed_for_every_other_company_country(string? country)
    {
        // 12-3456789 is a US employer identification number (EIN), not a Greek ΑΦΜ.
        VatNumberRules.Normalize("12-3456789", country).ShouldBe("123456789");
        VatNumberRules.Normalize("094259216", country).ShouldBe("094259216");
    }

    [Fact]
    public void Only_a_bare_9_digit_number_is_prefixed_even_for_a_Greek_company()
    {
        VatNumberRules.Normalize("12345678", "GR").ShouldBe("12345678");
        VatNumberRules.Normalize("1234567890", "GR").ShouldBe("1234567890");
        VatNumberRules.Normalize("DE123456789", "GR").ShouldBe("DE123456789");
    }

    // ---- Validation: general rule ----

    [Theory]
    [InlineData("123456789")] // US EIN
    [InlineData("GB123456789")] // UK
    [InlineData("GB123456789012")] // UK branch trader, 12 digits
    [InlineData("GBGD001")] // UK government department
    [InlineData("CHE123456789MWST")] // Switzerland
    [InlineData("CHE123456789TVA")]
    [InlineData("NO123456785MVA")] // Norway
    [InlineData("DE123456789")]
    [InlineData("FR12345678901")]
    [InlineData("BE0123456789")]
    [InlineData("ATU12345678")]
    [InlineData("NL123456789B01")]
    [InlineData("1234")] // shortest: 4
    [InlineData("12345678901234567890")] // longest: 20
    [InlineData("ABCD")]
    public void Validate_accepts_4_to_20_letters_or_digits_with_no_prefix_rule(string value) =>
        VatNumberRules.Validate(value).ShouldBeNull();

    [Fact]
    public void Validate_accepts_empty() => VatNumberRules.Validate(null).ShouldBeNull();

    [Theory]
    [InlineData("123")] // 3
    [InlineData("A1")]
    [InlineData("123456789012345678901")] // 21
    [InlineData("DE12 3456")] // not normalised: space
    [InlineData("DE-123456")] // not normalised: dash
    [InlineData("DE12_34")]
    [InlineData("DE123456789€")] // symbol
    [InlineData("ΔΕ123456")] // Greek letters, not Latin
    [InlineData("de123456789")] // not normalised: lower case
    public void Validate_rejects_other_characters_and_lengths_with_a_clear_message(string value) =>
        VatNumberRules.Validate(value).ShouldBe(VatNumberRules.InvalidMessage);

    // ---- Validation: strict only for EL ----

    [Theory]
    [InlineData("094259216")]
    [InlineData("123456783")]
    [InlineData("999999993")]
    [InlineData("000000050")] // sum mod 11 is 10, so the check digit is 0
    public void A_greek_AFM_with_the_right_check_digit_is_valid(string afm)
    {
        VatNumberRules.IsValidGreekAfm(afm).ShouldBeTrue();
        VatNumberRules.Validate("EL" + afm).ShouldBeNull();
    }

    [Theory]
    [InlineData("123456789")] // check digit should be 3
    [InlineData("094259217")] // one digit off
    [InlineData("094259261")] // two digits swapped
    [InlineData("099999990")] // check digit should be 9
    [InlineData("998877665")] // check digit should be 6
    [InlineData("000000000")] // passes the arithmetic, but is not a real number
    [InlineData("12345678")]
    [InlineData("1234567890")]
    [InlineData("12345678A")]
    public void A_greek_AFM_with_the_wrong_check_digit_or_shape_is_invalid(string afm) =>
        VatNumberRules.IsValidGreekAfm(afm).ShouldBeFalse();

    [Theory]
    [InlineData("EL123456789")]
    [InlineData("EL094259217")]
    [InlineData("EL000000000")]
    public void Validate_explains_a_failed_check_digit_for_greek_numbers(string value) =>
        VatNumberRules.Validate(value).ShouldBe(VatNumberRules.InvalidGreekCheckDigitMessage);

    [Theory]
    [InlineData("EL12345678")] // 8 digits
    [InlineData("EL1234567890")] // 10 digits
    [InlineData("ELABCDEFGHI")] // letters
    [InlineData("EL12345678A")]
    [InlineData("ELECTRO12345")] // anything starting with EL is held to the Greek rule
    [InlineData("EL")]
    public void Validate_requires_exactly_9_digits_after_EL(string value) =>
        VatNumberRules.Validate(value).ShouldBe(VatNumberRules.InvalidGreekMessage);

    [Fact]
    public void Other_countries_are_not_checked_for_a_greek_check_digit()
    {
        VatNumberRules.Validate("DE123456789").ShouldBeNull();
        VatNumberRules.Validate("123456789").ShouldBeNull(); // 123456789 would fail the Greek check digit
    }

    // ---- The form attribute ----

    [Theory]
    [InlineData("el 094.259-216", true)]
    [InlineData("094259216", true)] // the form does not know the country: valid as a general ID
    [InlineData("12-3456789", true)] // US EIN
    [InlineData("GB 123 4567 89", true)]
    [InlineData("CHE-123.456.789 MWST", true)]
    [InlineData("NO 123 456 785 MVA", true)]
    [InlineData("", true)]
    [InlineData("EL123456789", false)] // wrong Greek check digit
    [InlineData("EL12345", false)]
    [InlineData("123", false)]
    [InlineData("DE123456789€", false)]
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
