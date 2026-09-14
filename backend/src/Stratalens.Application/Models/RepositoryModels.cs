namespace Stratalens.Application.Models;

// DTOs relacionados con el proveedor de código fuente (GitHub, etc.).
// Son records inmutables: solo transportan datos entre capas, sin lógica.

// Resumen de un repositorio del usuario (para listarlos antes de conectar uno).
public record RepositorySummary(string Owner, string Name, string DefaultBranch, bool IsPrivate);

// Un archivo o carpeta dentro del árbol del repositorio.
// Type: "blob" (archivo) o "tree" (carpeta), siguiendo la nomenclatura de Git.
public record RepositoryFile(string Path, string Type);

// Coordenadas para ubicar un repositorio en una referencia concreta (rama o commit).
public record RepositoryReference(string Owner, string Name, string Reference);
