using Stratalens.Domain.Entities;
using Stratalens.Domain.Exceptions;

namespace Stratalens.Domain.Tests;

// Tests de las invariantes de Trace, el aggregate root de la telemetría (T14 / RULES.md).
// Estructura AAA. La invariante estrella — "exactamente un span raíz" — se prueba en sus
// tres estados: uno (válido), cero y dos (ambos deben fallar).
public class TraceTests
{
    private static readonly Guid Proj = Guid.NewGuid();
    private static readonly DateTime StartUtc = new(2026, 9, 11, 10, 0, 0, DateTimeKind.Utc);

    // Helper: crea un span raíz (sin padre).
    private static Span Root(string spanId = "root") =>
        new(spanId, null, "Frontend", "Backend", "GET /", StartUtc, 10, "OK");

    // Helper: crea un span hijo (con padre), por lo tanto no raíz.
    private static Span Child(string spanId, string parentSpanId) =>
        new(spanId, parentSpanId, "Backend", "PostgreSQL", "SELECT users", StartUtc, 3, "OK");

    // ---------- Casos válidos ----------

    [Fact]
    public void Create_ConUnRaizYVariosHijos_CreaTrace()
    {
        // Arrange
        var spans = new[] { Root(), Child("c1", "root"), Child("c2", "root") };

        // Act
        var trace = Trace.Create(Proj, "trace-abc", spans);

        // Assert
        Assert.NotEqual(Guid.Empty, trace.Id);
        Assert.Equal(Proj, trace.ProjectId);
        Assert.Equal("trace-abc", trace.TraceId);
        Assert.Equal(3, trace.Spans.Count);
    }

    [Fact]
    public void Create_ConUnSoloSpanRaiz_EsValido()
    {
        // El caso mínimo: un trace de un solo span, que es el raíz.
        var trace = Trace.Create(Proj, "trace-abc", new[] { Root() });
        Assert.Single(trace.Spans);
    }

    [Fact]
    public void Spans_EsColeccionDeSoloLectura()
    {
        // La colección se expone como IReadOnlyList: nadie de fuera puede mutarla y
        // saltarse la invariante del raíz. Confirmamos el contrato de tipo.
        var trace = Trace.Create(Proj, "trace-abc", new[] { Root() });
        Assert.IsAssignableFrom<IReadOnlyList<Span>>(trace.Spans);
    }

    // ---------- Casos que deben fallar ----------

    [Fact]
    public void Create_SinNingunSpanRaiz_LanzaDomainException()
    {
        // Todos los spans tienen padre => cero raíces. No hay recorrido con punto de inicio.
        var spans = new[] { Child("c1", "x"), Child("c2", "x") };
        Assert.Throws<DomainException>(() => Trace.Create(Proj, "trace-abc", spans));
    }

    [Fact]
    public void Create_ConDosSpansRaiz_LanzaDomainException()
    {
        // Dos spans sin padre => dos raíces. Un trace es UNA request, con un único inicio.
        var spans = new[] { Root("root-1"), Root("root-2") };
        Assert.Throws<DomainException>(() => Trace.Create(Proj, "trace-abc", spans));
    }

    [Fact]
    public void Create_SinSpans_LanzaDomainException()
    {
        Assert.Throws<DomainException>(() => Trace.Create(Proj, "trace-abc", Array.Empty<Span>()));
    }

    [Fact]
    public void Create_ConSpansNull_LanzaDomainException()
    {
        Assert.Throws<DomainException>(() => Trace.Create(Proj, "trace-abc", null!));
    }

    [Fact]
    public void Create_SinProjectId_LanzaDomainException()
    {
        Assert.Throws<DomainException>(() => Trace.Create(Guid.Empty, "trace-abc", new[] { Root() }));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_SinTraceId_LanzaDomainException(string? traceId)
    {
        Assert.Throws<DomainException>(() => Trace.Create(Proj, traceId!, new[] { Root() }));
    }
}
