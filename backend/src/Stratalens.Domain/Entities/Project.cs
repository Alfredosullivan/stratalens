using Stratalens.Domain.Exceptions;

namespace Stratalens.Domain.Entities;

// Unidad de trabajo de la app: agrupa un repositorio conectado, su grafo de
// arquitectura y su telemetría. En el MVP pertenece a un único usuario dueño
// (no hay equipos), por eso lleva OwnerUserId como invariante.
public class Project
{
    public Guid Id { get; }
    public Guid OwnerUserId { get; }   // dueño único del proyecto (regla de negocio del MVP)
    public string Name { get; } = null!;  // EF lo rellena al rehidratar
    public DateTime CreatedAt { get; } // siempre en UTC (ver constructor)

    // Los tres nullable, all-or-nothing: un Project manual (CreateManual) no tiene
    // ninguno; un Project desde GitHub (CreateFromRepository) tiene los tres. No existe
    // un tercer camino público para crear un Project, así que "los tres o ninguno" se
    // cumple por construcción, no por una validación que alguien podría olvidar repetir.
    public string? RepositoryOwner { get; }
    public string? RepositoryName { get; }
    public string? RepositoryReference { get; }

    // Constructor privado SOLO para EF Core (rehidratación desde la DB). Ver Edge.cs.
    private Project() { }

    // Constructor privado: la única forma de crear un Project NUEVO es a través de los
    // factory methods de abajo, que garantizan las invariantes según el origen (mismo
    // patrón que Edge.cs). No valida nada por sí mismo — validar aquí duplicaría lo que
    // ya valida cada factory y podría desalinearse con lo que cada uno necesita.
    private Project(
        Guid ownerUserId,
        string name,
        string? repositoryOwner,
        string? repositoryName,
        string? repositoryReference)
    {
        Id = Guid.NewGuid();
        OwnerUserId = ownerUserId;
        Name = name;
        RepositoryOwner = repositoryOwner;
        RepositoryName = repositoryName;
        RepositoryReference = repositoryReference;
        // UtcNow y no Now: guardar siempre en UTC evita bugs de zona horaria
        // cuando el backend, la DB y el usuario están en husos distintos.
        CreatedAt = DateTime.UtcNow;
    }

    // Crea un proyecto sin repositorio conectado (el usuario solo le puso un nombre).
    public static Project CreateManual(Guid ownerUserId, string name)
    {
        // Un proyecto sin dueño no puede existir: rompería el aislamiento entre usuarios.
        if (ownerUserId == Guid.Empty)
            throw new DomainException("Un Project debe tener un OwnerUserId válido.");

        if (string.IsNullOrWhiteSpace(name))
            throw new DomainException("Un Project debe tener un Name.");

        return new Project(ownerUserId, name, null, null, null);
    }

    // Crea un proyecto a partir de un repositorio de GitHub elegido por el usuario.
    // El Name se deriva del repo: no tiene sentido pedirle al usuario que lo escriba
    // dos veces si ya lo está eligiendo de una lista.
    public static Project CreateFromRepository(
        Guid ownerUserId,
        string repositoryOwner,
        string repositoryName,
        string repositoryReference)
    {
        if (ownerUserId == Guid.Empty)
            throw new DomainException("Un Project debe tener un OwnerUserId válido.");

        if (string.IsNullOrWhiteSpace(repositoryOwner))
            throw new DomainException("Un Project desde repositorio debe tener un RepositoryOwner.");

        if (string.IsNullOrWhiteSpace(repositoryName))
            throw new DomainException("Un Project desde repositorio debe tener un RepositoryName.");

        if (string.IsNullOrWhiteSpace(repositoryReference))
            throw new DomainException("Un Project desde repositorio debe tener un RepositoryReference.");

        return new Project(ownerUserId, repositoryName, repositoryOwner, repositoryName, repositoryReference);
    }
}
