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

    // Jerarquía (T33, patrón Adjacency List): id del nodo padre, o null si es raíz (ej.
    // Backend es raíz; un Controller dentro de él lleva aquí el Id del Backend). "Estar
    // dentro de" es composición estructural, NO un Edge (los edges son conexiones de
    // comportamiento — decisión de modelado, ver SDD/TASKS.md Fase 6 profundidad).
    // Un solo campo garantiza "máximo un padre" por construcción. La auto-referencia
    // (ser su propio padre) es imposible: el Id se genera dentro del ctor, así que nadie
    // puede pasar un ParentNodeId igual a un Id que aún no existe — por eso NO hay un check
    // (sería código muerto). Los ciclos A→B→A no son chequeables a nivel de un nodo suelto;
    // los previene el builder asignando padres de arriba hacia abajo (T34).
    public Guid? ParentNodeId { get; }

    // Constructor privado SOLO para EF Core (rehidratación desde la DB). Ver Edge.cs.
    private Node() { }

    // Constructor público que valida: es imposible obtener un Node sin nombre o sin tipo.
    public Node(
        Guid projectId,
        string name,
        string type,
        NodeCategory category,
        IReadOnlyDictionary<string, string>? metadata = null,
        Guid? parentNodeId = null)
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
        ParentNodeId = parentNodeId;
    }
}
