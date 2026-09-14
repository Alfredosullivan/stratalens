namespace Stratalens.Domain.Enums;

// Taxonomía CERRADA de categorías de nodo (Contexto maestro, sección 4).
// Es un enum (y no un string) porque este conjunto es estable y acotado:
// añadir una categoría nueva es una decisión de diseño, no un dato de entrada.
// En cambio, el Type concreto de un nodo (PostgreSQL, Redis, Stripe...) sí es
// un string abierto, para mantener la herramienta agnóstica al stack.
public enum NodeCategory
{
    Application,   // Frontend, Backend, API, Microservice, Mobile, Desktop
    Code,          // Component, Module, Controller, Service, Repository, Class
    Infrastructure,// Server, Container, Load Balancer, Gateway, CDN, Queue, Cache
    Database,      // PostgreSQL, MySQL, SQL Server, MongoDB, Redis...
    External,      // Stripe, Auth0, Firebase, SendGrid, APIs de terceros
    DevOps,        // GitHub, GitLab, GitHub Actions, Jenkins...
    Deployment     // Vercel, Railway, Render, AWS, Azure, GCP...
}
