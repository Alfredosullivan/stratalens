using Stratalens.Domain.Entities;
using Stratalens.Domain.Exceptions;

namespace Stratalens.Domain.Tests;

// Tests de las invariantes de Span (T14 / RULES.md). Estructura AAA (Arrange/Act/Assert).
// Incluyen tanto los caminos válidos como los que DEBEN fallar: un test que solo prueba
// el happy path no demuestra que la invariante realmente protege nada.
public class SpanTests
{
    // Fecha válida de referencia: en UTC, como exige la invariante.
    private static readonly DateTime StartUtc = new(2026, 9, 11, 10, 0, 0, DateTimeKind.Utc);

    // ---------- Casos válidos ----------

    [Fact]
    public void Constructor_ConDatosValidos_CreaSpan()
    {
        // Act
        var span = new Span("span-1", null, "Frontend", "Backend", "POST /api/login", StartUtc, 12.5, "OK");

        // Assert
        Assert.NotEqual(Guid.Empty, span.Id);
        Assert.Equal("span-1", span.SpanId);
        Assert.Equal("POST /api/login", span.Operation);
        Assert.Equal(12.5, span.DurationMs);
    }

    [Fact]
    public void Constructor_SinParentSpanId_MarcaSpanComoRaiz()
    {
        // ParentSpanId nulo => es el span raíz del trace.
        var span = new Span("root", null, "Frontend", "Backend", "GET /", StartUtc, 5, "OK");
        Assert.True(span.IsRoot);
    }

    [Fact]
    public void Constructor_ConParentSpanId_NoEsRaiz()
    {
        var span = new Span("child", "root", "Backend", "PostgreSQL", "SELECT users", StartUtc, 3, "OK");
        Assert.False(span.IsRoot);
    }

    [Fact]
    public void Constructor_ConDuracionCero_EsValido()
    {
        // Cero es el borde inferior válido: un tramo puede ser instantáneo, no negativo.
        var span = new Span("span-1", null, "Frontend", null, "ping", StartUtc, 0, "OK");
        Assert.Equal(0, span.DurationMs);
    }

    // ---------- Casos que deben fallar ----------

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_SinSpanId_LanzaDomainException(string? spanId)
    {
        Assert.Throws<DomainException>(() =>
            new Span(spanId!, null, "Frontend", "Backend", "GET /", StartUtc, 1, "OK"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_SinSourceNode_LanzaDomainException(string? sourceNode)
    {
        Assert.Throws<DomainException>(() =>
            new Span("span-1", null, sourceNode!, "Backend", "GET /", StartUtc, 1, "OK"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_SinOperation_LanzaDomainException(string? operation)
    {
        Assert.Throws<DomainException>(() =>
            new Span("span-1", null, "Frontend", "Backend", operation!, StartUtc, 1, "OK"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_SinStatus_LanzaDomainException(string? status)
    {
        Assert.Throws<DomainException>(() =>
            new Span("span-1", null, "Frontend", "Backend", "GET /", StartUtc, 1, status!));
    }

    [Theory]
    [InlineData(-0.1)]
    [InlineData(-1)]
    [InlineData(-1000)]
    public void Constructor_ConDuracionNegativa_LanzaDomainException(double durationMs)
    {
        Assert.Throws<DomainException>(() =>
            new Span("span-1", null, "Frontend", "Backend", "GET /", StartUtc, durationMs, "OK"));
    }

    [Fact]
    public void Constructor_ConStartedAtLocal_LanzaDomainException()
    {
        // Kind=Local: rechazado. Convertirlo asumiría un huso y corrompería la duración.
        var local = new DateTime(2026, 9, 11, 10, 0, 0, DateTimeKind.Local);
        Assert.Throws<DomainException>(() =>
            new Span("span-1", null, "Frontend", "Backend", "GET /", local, 1, "OK"));
    }

    [Fact]
    public void Constructor_ConStartedAtUnspecified_LanzaDomainException()
    {
        // Kind=Unspecified es la trampa clásica: no lleva huso, así que también se rechaza.
        var unspecified = new DateTime(2026, 9, 11, 10, 0, 0, DateTimeKind.Unspecified);
        Assert.Throws<DomainException>(() =>
            new Span("span-1", null, "Frontend", "Backend", "GET /", unspecified, 1, "OK"));
    }
}
