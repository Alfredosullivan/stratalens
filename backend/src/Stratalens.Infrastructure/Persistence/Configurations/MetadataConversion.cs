using System.Text.Json;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Stratalens.Infrastructure.Persistence.Configurations;

// Convierte el Metadata (diccionario de solo lectura) a/desde JSON para la columna jsonb.
// Se reutiliza en Node y Edge para no duplicar la lógica de mapeo.
internal static class MetadataConversion
{
    private static readonly JsonSerializerOptions Options = new();

    // Converter: cómo se guarda (a JSON) y cómo se recupera (a diccionario).
    public static readonly ValueConverter<IReadOnlyDictionary<string, string>, string> Converter =
        new(
            dict => JsonSerializer.Serialize(dict, Options),
            json => JsonSerializer.Deserialize<Dictionary<string, string>>(json, Options)
                    ?? new Dictionary<string, string>());

    // Comparer: EF necesita saber comparar y "fotografiar" el diccionario para
    // detectar cambios. Sin esto, EF no rastrea bien un tipo de referencia mutable.
    public static readonly ValueComparer<IReadOnlyDictionary<string, string>> Comparer =
        new(
            (a, b) => JsonSerializer.Serialize(a, Options) == JsonSerializer.Serialize(b, Options),
            v => JsonSerializer.Serialize(v, Options).GetHashCode(),
            v => (IReadOnlyDictionary<string, string>)v.ToDictionary(kv => kv.Key, kv => kv.Value));
}
