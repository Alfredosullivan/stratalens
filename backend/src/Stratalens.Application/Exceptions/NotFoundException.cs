namespace Stratalens.Application.Exceptions;

// El recurso no existe O no es accesible por el usuario actual. La Api la traduce a 404.
// Unificar ambos casos en un solo error (y en 404, no 403) evita filtrar la EXISTENCIA
// de recursos ajenos: un usuario no puede distinguir "no existe" de "no es tuyo".
public class NotFoundException : Exception
{
    public NotFoundException(string message) : base(message)
    {
    }
}
