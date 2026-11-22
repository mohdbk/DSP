using Dsp.EServices.Domain.Catalog;

namespace Dsp.UnitTests.EServices;

public class FormValidatorTests
{
    private static ServiceDefinition Service(params FieldDefinition[] fields) => new()
    {
        Code = "TEST",
        Name = "Test",
        Description = "Test service",
        Steps = [new FormStep { Id = "one", Title = "One", Fields = fields }],
    };

    private static Dictionary<string, string?> Data(params (string Key, string? Value)[] values) =>
        values.ToDictionary(v => v.Key, v => v.Value);

    [Fact]
    public void Missing_required_field_is_reported()
    {
        var service = Service(new FieldDefinition { Key = "name", Label = "Name", Required = true });

        var errors = FormValidator.Validate(service, Data());

        var error = Assert.Single(errors);
        Assert.Equal("name", error.Field);
        Assert.Contains("required", error.Message);
    }

    [Fact]
    public void Optional_empty_field_is_valid()
    {
        var service = Service(new FieldDefinition { Key = "notes", Label = "Notes" });

        Assert.Empty(FormValidator.Validate(service, Data(("notes", ""))));
    }

    [Theory]
    [InlineData("not-an-email", false)]
    [InlineData("someone@example.ae", true)]
    public void Email_must_be_valid(string value, bool valid)
    {
        var service = Service(new FieldDefinition { Key = "email", Label = "Email", Type = FieldTypes.Email, Required = true });

        Assert.Equal(valid, FormValidator.Validate(service, Data(("email", value))).Count == 0);
    }

    [Theory]
    [InlineData("784-1990-1234567-1", true)]
    [InlineData("784199012345671", false)]
    [InlineData("123-1990-1234567-1", false)]
    public void Pattern_is_enforced_with_custom_message(string value, bool valid)
    {
        var service = Service(new FieldDefinition
        {
            Key = "emiratesId",
            Label = "Emirates ID",
            Required = true,
            Pattern = @"^784-\d{4}-\d{7}-\d$",
            PatternMessage = "Enter the Emirates ID as 784-YYYY-NNNNNNN-N.",
        });

        var errors = FormValidator.Validate(service, Data(("emiratesId", value)));

        Assert.Equal(valid, errors.Count == 0);
        if (!valid) Assert.Equal("Enter the Emirates ID as 784-YYYY-NNNNNNN-N.", errors[0].Message);
    }

    [Fact]
    public void Select_accepts_only_listed_values()
    {
        var service = Service(new FieldDefinition
        {
            Key = "emirate",
            Label = "Emirate",
            Type = FieldTypes.Select,
            Required = true,
            Options = [new FieldOption { Value = "DXB", Label = "Dubai" }, new FieldOption { Value = "AUH", Label = "Abu Dhabi" }],
        });

        Assert.Empty(FormValidator.Validate(service, Data(("emirate", "DXB"))));
        Assert.Single(FormValidator.Validate(service, Data(("emirate", "Dubai"))));
    }

    [Theory]
    [InlineData("2027-01-31", true)]
    [InlineData("31/01/2027", false)]
    [InlineData("2027-02-30", false)]
    public void Dates_use_iso_format(string value, bool valid)
    {
        var service = Service(new FieldDefinition { Key = "start", Label = "Start", Type = FieldTypes.Date, Required = true });

        Assert.Equal(valid, FormValidator.Validate(service, Data(("start", value))).Count == 0);
    }

    [Fact]
    public void Number_respects_min_and_max()
    {
        var service = Service(new FieldDefinition { Key = "count", Label = "Count", Type = FieldTypes.Number, Min = 1, Max = 10 });

        Assert.Empty(FormValidator.Validate(service, Data(("count", "5"))));
        Assert.Single(FormValidator.Validate(service, Data(("count", "0"))));
        Assert.Single(FormValidator.Validate(service, Data(("count", "11"))));
        Assert.Single(FormValidator.Validate(service, Data(("count", "abc"))));
    }

    [Fact]
    public void Required_checkbox_must_be_true()
    {
        var service = Service(new FieldDefinition { Key = "declaration", Label = "I confirm", Type = FieldTypes.Checkbox, Required = true });

        Assert.Single(FormValidator.Validate(service, Data(("declaration", ""))));
        Assert.Empty(FormValidator.Validate(service, Data(("declaration", "true"))));
    }

    [Fact]
    public void File_field_needs_an_uploaded_document_id()
    {
        var service = Service(new FieldDefinition { Key = "file", Label = "Agreement", Type = FieldTypes.File, Required = true });

        Assert.Single(FormValidator.Validate(service, Data(("file", "agreement.pdf"))));
        Assert.Empty(FormValidator.Validate(service, Data(("file", Guid.NewGuid().ToString()))));
    }

    [Fact]
    public void Partial_validation_checks_only_given_keys_and_flags_unknown_fields()
    {
        var service = Service(
            new FieldDefinition { Key = "a", Label = "A", Required = true },
            new FieldDefinition { Key = "b", Label = "B", Required = true });

        var errors = FormValidator.Validate(service, Data(("a", "x"), ("zzz", "y")), ["a", "zzz"]);

        var error = Assert.Single(errors);
        Assert.Equal("zzz", error.Field);
    }

    [Fact]
    public void Length_limits_are_enforced()
    {
        var service = Service(new FieldDefinition { Key = "name", Label = "Name", MinLength = 3, MaxLength = 5 });

        Assert.Single(FormValidator.Validate(service, Data(("name", "ab"))));
        Assert.Single(FormValidator.Validate(service, Data(("name", "abcdef"))));
        Assert.Empty(FormValidator.Validate(service, Data(("name", "abcd"))));
    }
}
