using Stratalens.Domain.Exceptions;

namespace Stratalens.Domain.Entities;

// Usuario de la app. En el MVP la identidad viene de GitHub, así que guarda el id
// y el login de GitHub. El access_token NO vive aquí: es un secreto de infraestructura
// (se guarda cifrado, como shadow property gestionada solo por Infrastructure) — el
// dominio no debe conocer detalles de cifrado.
//
// NOTA (ADR de ARCHITECTURE.md): si mañana se soportan GitLab/Bitbucket, se extrae
// la identidad de GitHub a una entidad aparte. Para el MVP (un solo proveedor) se
// mantiene en User por simplicidad.
public class User
{
    public Guid Id { get; }
    public string GitHubUserId { get; } = null!;  // id numérico estable de GitHub (como string)
    public string GitHubLogin { get; } = null!;   // username de GitHub
    public DateTime CreatedAt { get; }

    // Constructor privado SOLO para EF Core (rehidratación). Ver Edge.cs.
    private User() { }

    public User(string gitHubUserId, string gitHubLogin)
    {
        if (string.IsNullOrWhiteSpace(gitHubUserId))
            throw new DomainException("Un User debe tener un GitHubUserId.");

        if (string.IsNullOrWhiteSpace(gitHubLogin))
            throw new DomainException("Un User debe tener un GitHubLogin.");

        Id = Guid.NewGuid();
        GitHubUserId = gitHubUserId;
        GitHubLogin = gitHubLogin;
        CreatedAt = DateTime.UtcNow;
    }
}
