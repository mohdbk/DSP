using System.Text.Json;
using Dsp.SharedKernel;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Dsp.BuildingBlocks.Persistence;

/// <summary>Stores a language map as a JSON text column. Plain text already in the column reads as English.</summary>
public static class LocalizedTextConversion
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public static PropertyBuilder<LocalizedText> IsLocalizedText(this PropertyBuilder<LocalizedText> property) =>
        property.HasConversion(
                v => v.ToJson(),
                s => LocalizedText.Parse(s),
                new ValueComparer<LocalizedText>(
                    (a, b) => JsonSerializer.Serialize(a, Json) == JsonSerializer.Serialize(b, Json),
                    v => JsonSerializer.Serialize(v, Json).GetHashCode(),
                    v => new LocalizedText(v)))
            .IsRequired();
}
