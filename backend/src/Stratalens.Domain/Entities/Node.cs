using Stratalens.Domain.Enums;
using Stratalens.Domain.Exceptions;

namespace Stratalens.Domain.Entities;

// Representa un componente del sistema en el grafo de arquitectura
// (frontend, backend, base de datos, servicio externo, etc.).
// A diferencia de Edge no tiene dos orígenes distintos de creación,
// así que usa un constructor validador simple (no factory methods).
public class Node
{
    // Solo lectura: un Node no muta tras crearse, igual que Edge.
    public Guid Id { get; }
    public Guid ProjectId { get; }
    public string Name { get; } = null!;   // ej. "AuthService" (EF lo rellena al rehidratar)
    public string Type { get; } = null!;   // ej. "PostgreSQL", "AspNetCoreApi" (EF lo rellena)
    public NodeCategory Category { get; }
    public IReadOnlyDictionary<string, string> Metadata { get; } = new Dictionary<string, string>();

    // Constructor privado SOLO para EF Core (rehidratación desde la DB). Ver Edge.cs.
    private Node() { }

    // Constructor público que valida: es imposible obtener un Node sin nombre o sin tipo.
    public Node(
        Guid projectId,
        string name,
        string type,
        NodeCategory category,
        IReadOnlyDictionary<string, string>? metadata = null)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new DomainException("Un Node debe tener un Name.");

        if (string.IsNullOrWhiteSpace(type))
            throw new DomainException("Un Node debe tener un Type (ej. 'PostgreSQL', 'AspNetCoreApi').");

        Id = Guid.NewGuid();
        ProjectId = projectId;
        Name = name;
        Type = type;
        Category = category;
        Metadata = metadata ?? new Dictionary<string, string>();
    }
}
