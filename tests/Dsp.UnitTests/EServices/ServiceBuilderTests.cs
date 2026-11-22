using Dsp.EServices.Application;
using Dsp.EServices.Domain.Catalog;
using Dsp.EServices.Infrastructure.Catalog;
using Dsp.SharedKernel;

namespace Dsp.UnitTests.EServices;

public class ServiceDefinitionRulesTests
{
    internal static ServiceDefinition Valid(string code = "BUSINESS_PERMIT", int workflowVersion = 1) => new()
    {
        Code = code,
        Name = "Business Permit",
        NameAr = "تصريح تجاري",
        Description = "Apply for a business permit.",
        DescriptionAr = "تقدم بطلب تصريح تجاري.",
        Fee = 150,
        ReferenceField = "businessName",
        WorkflowVersion = workflowVersion,
        ReviewChecklist = ["The business name is appropriate."],
        Steps =
        [
            new FormStep
            {
                Id = "details", Title = "Details", TitleAr = "التفاصيل",
                Fields =
                [
                    new FieldDefinition { Key = "businessName", Label = "Business name", LabelAr = "اسم النشاط", Required = true },
                    new FieldDefinition
                    {
                        Key = "emirate", Label = "Emirate", LabelAr = "الإمارة", Type = FieldTypes.Select, Required = true,
                        Options = [new FieldOption { Value = "DXB", Label = "Dubai", LabelAr = "دبي" }],
                    },
                ],
            },
        ],
    };

    [Fact]
    public void A_complete_bilingual_definition_is_valid() => Assert.Empty(ServiceDefinitionRules.Validate(Valid()));

    [Fact]
    public void The_shipped_services_are_publishable()
    {
        foreach (var service in new EmbeddedServiceCatalog().GetAll())
            Assert.Empty(ServiceDefinitionRules.Validate(service));
    }

    [Fact]
    public void Errors_point_at_the_place_in_the_definition()
    {
        var d = Valid("bad code");
        var broken = new ServiceDefinition
        {
            Code = d.Code, Name = d.Name, NameAr = null, Description = d.Description, DescriptionAr = d.DescriptionAr,
            Fee = 0, ReferenceField = "nope", WorkflowVersion = 7,
            Steps =
            [
                new FormStep
                {
                    Id = "details", Title = "Details", TitleAr = "التفاصيل",
                    Fields =
                    [
                        new FieldDefinition { Key = "name", Label = "Name", LabelAr = "الاسم" },
                        new FieldDefinition { Key = "NAME", Label = "Name again", LabelAr = "الاسم" },
                        new FieldDefinition { Key = "kind", Label = "Kind", LabelAr = "النوع", Type = FieldTypes.Select },
                        new FieldDefinition { Key = "code", Label = "Code", LabelAr = "الرمز", Pattern = "([a-z" },
                        new FieldDefinition { Key = "who", Label = "Who", Type = FieldTypes.Text, PrefillFrom = "salary" },
                    ],
                },
            ],
        };

        var at = ServiceDefinitionRules.Validate(broken).Select(e => e.Field).ToList();

        Assert.Contains("code", at);
        Assert.Contains("nameAr", at);
        Assert.Contains("fee", at);
        Assert.Contains("workflowVersion", at);
        Assert.Contains("referenceField", at);
        Assert.Contains("steps[0].fields[1].key", at);      // duplicate key (case-insensitive)
        Assert.Contains("steps[0].fields[2].options", at);  // select without options
        Assert.Contains("steps[0].fields[3].pattern", at);  // invalid regular expression
        Assert.Contains("steps[0].fields[4].labelAr", at);
        Assert.Contains("steps[0].fields[4].prefillFrom", at);
    }
}

public class ServiceVersionTests
{
    [Fact]
    public void A_draft_is_published_and_keeps_its_version_number()
    {
        var draft = ServiceVersion.NewDraft(ServiceDefinitionRulesTests.Valid(), 3, "Khalid");
        draft.Publish("Khalid");

        Assert.Equal(ServiceVersionStatus.Published, draft.Status);
        Assert.Equal(3, draft.ToDefinition().Version);
        Assert.Equal("Khalid", draft.PublishedBy);
    }

    [Fact]
    public void Publishing_is_refused_while_the_definition_has_errors()
    {
        var incomplete = ServiceDefinitionRulesTests.Valid();
        var draft = ServiceVersion.NewDraft(new ServiceDefinition
        {
            Code = incomplete.Code, Name = incomplete.Name, Description = incomplete.Description, Fee = incomplete.Fee,
            Steps = incomplete.Steps,
        }, 1, "Khalid");

        var ex = Assert.Throws<ValidationException>(() => draft.Publish("Khalid"));
        Assert.Contains(ex.Errors, e => e.Field == "nameAr");
        Assert.Equal(ServiceVersionStatus.Draft, draft.Status);
    }

    [Fact]
    public void A_published_version_never_changes()
    {
        var version = ServiceVersion.NewDraft(ServiceDefinitionRulesTests.Valid(), 1, "Khalid");
        version.Publish("Khalid");

        Assert.Throws<DomainException>(() => version.UpdateDraft(ServiceDefinitionRulesTests.Valid(), "Khalid"));
    }

    [Fact]
    public void The_code_of_a_draft_cannot_change()
    {
        var draft = ServiceVersion.NewDraft(ServiceDefinitionRulesTests.Valid(), 1, "Khalid");

        Assert.Throws<DomainException>(() => draft.UpdateDraft(ServiceDefinitionRulesTests.Valid("OTHER_CODE"), "Khalid"));
    }
}

public class ServiceBuilderServiceTests
{
    private static readonly Caller Staff = new("staff:khalid", "Khalid", true, new Dictionary<string, string?>());
    private static readonly Caller Citizen = new("uaepass:abc", "Fatima", false, new Dictionary<string, string?>());

    private readonly InMemoryVersions _versions = new();
    private readonly ServiceBuilderService _builder;

    public ServiceBuilderServiceTests() => _builder = new ServiceBuilderService(_versions);

    [Fact]
    public async Task A_new_service_is_a_draft_until_published()
    {
        var created = await _builder.CreateAsync(Staff, ServiceDefinitionRulesTests.Valid(), default);

        Assert.NotNull(created.Draft);
        Assert.Null(created.Published);
        Assert.Null(await _versions.FindPublishedAsync("BUSINESS_PERMIT", default));

        var published = await _builder.PublishAsync(Staff, "BUSINESS_PERMIT", default);

        Assert.Equal(1, published.Published!.Version);
        Assert.Null(published.Draft);
        Assert.NotNull(await _versions.FindPublishedAsync("BUSINESS_PERMIT", default));
    }

    [Fact]
    public async Task Editing_a_live_service_creates_the_next_version_and_publishing_retires_the_old_one()
    {
        await _builder.CreateAsync(Staff, ServiceDefinitionRulesTests.Valid(), default);
        await _builder.PublishAsync(Staff, "BUSINESS_PERMIT", default);

        var edited = await _builder.SaveDraftAsync(Staff, "BUSINESS_PERMIT",
            ServiceDefinitionRulesTests.Valid(workflowVersion: WorkflowVersions.AiAssisted), default);

        Assert.Equal(2, edited.Draft!.Version);
        Assert.Equal(1, edited.Published!.Version); // citizens still see v1 while v2 is edited

        var after = await _builder.PublishAsync(Staff, "BUSINESS_PERMIT", default);

        Assert.Equal(2, after.Published!.Version);
        Assert.Equal(WorkflowVersions.AiAssisted, after.Published.WorkflowVersion);
        Assert.Equal(new[] { "Published", "Retired" }, after.Versions.Select(v => v.Status));
        Assert.NotNull(await _versions.FindVersionAsync("BUSINESS_PERMIT", 1, default)); // apps on v1 can still read it
    }

    [Fact]
    public async Task Duplicate_codes_and_citizens_are_refused()
    {
        await _builder.CreateAsync(Staff, ServiceDefinitionRulesTests.Valid(), default);

        await Assert.ThrowsAsync<ValidationException>(() => _builder.CreateAsync(Staff, ServiceDefinitionRulesTests.Valid(), default));
        await Assert.ThrowsAsync<ForbiddenException>(() => _builder.ListAsync(Citizen, default));
    }

    [Fact]
    public async Task Discarding_a_never_published_service_removes_it()
    {
        await _builder.CreateAsync(Staff, ServiceDefinitionRulesTests.Valid(), default);

        Assert.Null(await _builder.DiscardDraftAsync(Staff, "BUSINESS_PERMIT", default));
        Assert.Empty(await _builder.ListAsync(Staff, default));
    }

    [Fact]
    public async Task The_shipped_services_are_seeded_once()
    {
        var seeder = new CatalogSeeder(_versions);
        var shipped = new EmbeddedServiceCatalog().GetAll();

        Assert.Equal(shipped.Count, await seeder.SeedAsync(shipped, default));
        Assert.Equal(0, await seeder.SeedAsync(shipped, default));
        Assert.Equal(shipped.Count, (await _versions.ListPublishedAsync(default)).Count);
    }

    internal sealed class InMemoryVersions : IServiceVersionRepository, IServiceCatalog
    {
        private readonly List<ServiceVersion> _items = [];

        public Task<IReadOnlyList<ServiceVersion>> ListAllAsync(CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<ServiceVersion>>(_items.ToList());

        public Task<IReadOnlyList<ServiceVersion>> ListForCodeAsync(string code, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<ServiceVersion>>(_items.Where(v => v.Code == code)
                .OrderByDescending(v => v.Version).ToList());

        public Task<bool> AnyAsync(CancellationToken ct) => Task.FromResult(_items.Count > 0);

        public Task AddAsync(ServiceVersion version, CancellationToken ct)
        {
            _items.Add(version);
            return Task.CompletedTask;
        }

        public void Remove(ServiceVersion version) => _items.Remove(version);

        public Task SaveChangesAsync(CancellationToken ct) => Task.CompletedTask;

        public Task<IReadOnlyList<ServiceDefinition>> ListPublishedAsync(CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<ServiceDefinition>>(_items.Where(v => v.Status == ServiceVersionStatus.Published)
                .Select(v => v.ToDefinition()).ToList());

        public Task<ServiceDefinition?> FindPublishedAsync(string code, CancellationToken ct) =>
            Task.FromResult(_items.FirstOrDefault(v => v.Code == code && v.Status == ServiceVersionStatus.Published)?.ToDefinition());

        public Task<ServiceDefinition?> FindVersionAsync(string code, int version, CancellationToken ct) =>
            Task.FromResult(_items.FirstOrDefault(v => v.Code == code && v.Version == version &&
                                                       v.Status != ServiceVersionStatus.Draft)?.ToDefinition());
    }
}
