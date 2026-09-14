namespace Stratalens.Domain.Exceptions;

// Excepción para violaciones de invariantes del dominio.
// La usamos para separar los errores de "regla de negocio rota"
// (ej. crear un Edge inválido) de errores técnicos genéricos.
// Más adelante, un middleware la traducirá a un 400/409 en la capa Api.
public class DomainException : Exception
{
    public DomainException(string message) : base(message)
    {
    }
}
