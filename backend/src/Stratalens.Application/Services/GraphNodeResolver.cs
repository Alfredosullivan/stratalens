using Stratalens.Domain.Entities;

namespace Stratalens.Application.Services;

// Resuelve un nombre de nodo de un trace (string de OTel, ej. "demo-backend",
// "postgresql") al Node del grafo correspondiente. Convención Opción A (T20):
// match EXACTO case-insensitive contra Name, Type o el alias de metadata "otelName".
// Si no hay match → null, y el promotor NO crea un edge (nunca inventar una relación).
public class GraphNodeResolver
{
    private readonly Dictionary<string, Node> _byKey;

    public GraphNodeResolver(IEnumerable<Node> nodes)
    {
        // OrdinalIgnoreCase = comparación exacta pero sin distinguir mayúsculas:
        // "postgresql" casa con el nodo "PostgreSQL".
        _byKey = new Dictionary<string, Node>(StringComparer.OrdinalIgnoreCase);

        foreach (var node in nodes)
        {
            Index(node.Name, node);
            Index(node.Type, node);
            if (node.Metadata.TryGetValue("otelName", out var alias))
                Index(alias, node);
        }
    }

    // TryAdd: si dos nodos comparten clave (ej. mismo Type), gana el primero y el resto
    // queda ambiguo — no se resuelve a un match "adivinado". Aceptable en el MVP.
    private void Index(string? key, Node node)
    {
        if (!string.IsNullOrWhiteSpace(key))
            _byKey.TryAdd(key.Trim(), node);
    }

    public Node? Resolve(string? traceName)
        => !string.IsNullOrWhiteSpace(traceName) && _byKey.TryGetValue(traceName.Trim(), out var node)
            ? node
            : null;
}
