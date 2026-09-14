namespace Stratalens.Api.Contracts;

// Repo de GitHub del usuario autenticado (T25). No exponemos RepositorySummary de
// Application directo (regla de RULES.md): aunque hoy tenga la misma forma, son
// contratos distintos con propósitos distintos (uno es HTTP público, el otro un
// modelo interno que Application podría cambiar sin que eso deba romper el cliente).
public record RepositorySummaryDto(string Owner, string Name, string DefaultBranch, bool IsPrivate);
