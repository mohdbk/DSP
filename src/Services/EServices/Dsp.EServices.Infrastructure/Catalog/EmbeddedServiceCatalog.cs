using System.Text.Json;
using Dsp.EServices.Application;
using Dsp.EServices.Domain.Catalog;

namespace Dsp.EServices.Infrastructure.Catalog;

/// <summary>
/// Loads service definitions shipped with the service (Catalog/Definitions/*.json).
/// Each file is one e-service, as produced by the OutSystems extraction pipeline.
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
}
