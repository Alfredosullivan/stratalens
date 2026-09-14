namespace Stratalens.Application.Exceptions;

// Una operación violaría una invariante de unicidad de negocio (ej. dos proyectos para
// el mismo repo del mismo usuario, T26). La Api la traduce a 409.
public class ConflictException : Exception
{
    public ConflictException(string message) : base(message)
    {
    }
}
