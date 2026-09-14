namespace Stratalens.Infrastructure.Providers;

// Excepción técnica para fallos al hablar con un proveedor de código (GitHub, etc.).
// A diferencia de DomainException (regla de negocio rota), esta representa un problema
// de infraestructura: la API respondió con error, no se pudo leer el repo, etc.
//
// Regla de seguridad (RULES.md): el mensaje NUNCA incluye el access_token. Solo el
// método HTTP, el endpoint y el status code, que son datos no sensibles.
public class ProviderConnectorException : Exception
{
    public ProviderConnectorException(string message) : base(message)
    {
    }
}
