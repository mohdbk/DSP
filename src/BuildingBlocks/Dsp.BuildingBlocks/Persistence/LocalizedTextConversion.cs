using System.Text.Json;
using Dsp.SharedKernel;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Dsp.BuildingBlocks.Persistence;

/// <summary>Stores a language map as a JSON column.</summary>
public static class LocalizedTextConversion
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public static PropertyBuilder<LocalizedText> IsLocalizedText(this PropertyBuilder<LocalizedText> property) =>
        property.HasConversion(
                v => JsonSerializer.Serialize(v, Json),
                s => JsonSerializer.Deserialize<LocalizedText>(s, Json) ?? new LocalizedText(),
                new ValueComparer<LocalizedText>(
                    (a, b) => JsonSerializer.Serialize(a, Json) == JsonSerializer.Serialize(b, Json),
                    v => JsonSerializer.Serialize(v, Json).GetHashCode(),
                    v => new LocalizedText(v)))
            .IsRequired();
}
