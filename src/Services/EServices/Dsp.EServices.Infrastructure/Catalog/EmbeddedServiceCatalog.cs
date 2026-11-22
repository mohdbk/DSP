using System.Text.Json;
using Dsp.EServices.Application;
using Dsp.EServices.Domain.Catalog;

namespace Dsp.EServices.Infrastructure.Catalog;

/// <summary>
/// The service definitions shipped with the platform (Catalog/Definitions/*.json), as produced by the
/// OutSystems extraction pipeline. They seed the database catalog on first start; after that the
/// service builder owns them. Also usable as a read-only catalog in tests.
/// </summary>
public sealed class EmbeddedServiceCatalog : IServiceCatalog
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private readonly IReadOnlyList<ServiceDefinition> _services;

    public EmbeddedServiceCatalog()
    {
        var assembly = typeof(EmbeddedServiceCatalog).Assembly;
        _services = assembly.GetManifestResourceNames()
            .Where(n => n.Contains(".Catalog.Definitions.") && n.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
            .Select(n =>
            {
                using var stream = assembly.GetManifestResourceStream(n)!;
                return JsonSerializer.Deserialize<ServiceDefinition>(stream, Json)
                       ?? throw new InvalidOperationException($"Invalid service definition {n}");
            })
            .OrderBy(s => s.Name)
            .ToList();
    }

    public IReadOnlyList<ServiceDefinition> GetAll() => _services;

    public ServiceDefinition? Find(string code) =>
        _services.FirstOrDefault(s => string.Equals(s.Code, code, StringComparison.OrdinalIgnoreCase));

    public Task<IReadOnlyList<ServiceDefinition>> ListPublishedAsync(CancellationToken ct) => Task.FromResult(_services);

    public Task<ServiceDefinition?> FindPublishedAsync(string code, CancellationToken ct) => Task.FromResult(Find(code));

    public Task<ServiceDefinition?> FindVersionAsync(string code, int version, CancellationToken ct) =>
        Task.FromResult(Find(code) is { } s && s.Version == version ? s : null);
}
