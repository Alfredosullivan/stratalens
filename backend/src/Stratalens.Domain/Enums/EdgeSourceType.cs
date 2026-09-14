namespace Stratalens.Domain.Enums;

// Distingue el origen de un Edge:
//  - Static:  inferido analizando código/configuración (nunca es un hecho confirmado).
//  - Runtime: confirmado por un trace real de OpenTelemetry (hecho observado).
// Esta distinción es la base de la regla "Static Discovery vs Runtime Observability"
// (PRODUCT.md) y de la invariante de Confidence en Edge.
public enum EdgeSourceType
{
    Static,
    Runtime
}
