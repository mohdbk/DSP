using Dsp.EServices.Domain.Catalog;
using Dsp.EServices.Infrastructure.Catalog;

namespace Dsp.UnitTests.EServices;

/// <summary>Guards every service definition (including ones added by the extraction pipeline).</summary>
public class ServiceCatalogTests
{
    private static readonly IReadOnlyList<ServiceDefinition> Services = new EmbeddedServiceCatalog().GetAll();
    private static readonly string[] KnownProfileClaims = ["name", "emirates_id", "email", "mobile"];

    public static TheoryData<string> ServiceCodes()
    {
        var data = new TheoryData<string>();
        foreach (var s in Services) data.Add(s.Code);
        return data;
    }

    private static ServiceDefinition Get(string code) => Services.Single(s => s.Code == code);

    [Fact]
    public void Catalog_loads_the_sample_services()
    {
        Assert.Contains(Services, s => s.Code == "TRADE_NAME_RESERVATION");
        Assert.Contains(Services, s => s.Code == "COMMERCIAL_AGENCY_REGISTRATION");
        Assert.Equal(Services.Count, Services.Select(s => s.Code).Distinct().Count());
    }

    [Theory]
    [MemberData(nameof(ServiceCodes))]
    public void Field_keys_are_unique(string code)
    {
        var keys = Get(code).AllFields.Select(f => f.Key.ToLowerInvariant()).ToList();
        Assert.Equal(keys.Count, keys.Distinct().Count());
    }

    [Theory]
    [MemberData(nameof(ServiceCodes))]
    public void Every_step_has_fields_and_every_select_has_options(string code)
    {
        var service = Get(code);
        Assert.All(service.Steps, s => Assert.NotEmpty(s.Fields));
        Assert.All(service.AllFields.Where(f => f.Type == FieldTypes.Select), f => Assert.NotEmpty(f.Options!));
    }

    [Theory]
    [MemberData(nameof(ServiceCodes))]
    public void Reference_field_and_prefill_sources_exist(string code)
    {
        var service = Get(code);
        if (service.ReferenceField is not null) Assert.NotNull(service.FindField(service.ReferenceField));
        Assert.All(service.AllFields.Where(f => f.PrefillFrom is not null),
            f => Assert.Contains(f.PrefillFrom, KnownProfileClaims));
    }

    [Theory]
    [MemberData(nameof(ServiceCodes))]
    public void Field_types_and_patterns_are_valid(string code)
    {
        var known = new[] { "text", "textarea", "email", "phone", "number", "date", "select", "file", "checkbox" };
        foreach (var field in Get(code).AllFields)
        {
            Assert.Contains(field.Type, known);
            if (field.Pattern is not null) _ = new System.Text.RegularExpressions.Regex(field.Pattern);
        }
    }

    [Theory]
    [MemberData(nameof(ServiceCodes))]
    public void Fee_is_positive(string code) => Assert.True(Get(code).Fee > 0);
}
