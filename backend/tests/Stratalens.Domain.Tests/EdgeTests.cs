using Stratalens.Domain.Entities;
using Stratalens.Domain.Enums;
using Stratalens.Domain.Exceptions;

namespace Stratalens.Domain.Tests;

// Tests de las invariantes de Edge (RULES.md). Estructura AAA (Arrange/Act/Assert).
// Incluyen tanto los caminos válidos como los que DEBEN fallar: un test que solo
// prueba el happy path no demuestra que la invariante realmente protege nada.
public class EdgeTests
{
    private static readonly Guid Proj = Guid.NewGuid();
    private static readonly Guid N1 = Guid.NewGuid();
    private static readonly Guid N2 = Guid.NewGuid();

    // ---------- FromStaticAnalysis: casos válidos ----------

    [Fact]
    public void FromStaticAnalysis_ConDatosValidos_CreaEdgeEstatico()
    {
        // Act
        var edge = Edge.FromStaticAnalysis(Proj, N1, N2, "HTTP/REST", "src/services/api.ts", 90);

        // Assert
        Assert.Equal(EdgeSourceType.Static, edge.SourceType);
        Assert.Equal(90, edge.Confidence);
        Assert.Equal("src/services/api.ts", edge.Source);
        Assert.NotEqual(Guid.Empty, edge.Id);
    }

    [Fact]
    public void FromStaticAnalysis_ConConfianza99_EsValido()
    {
        // 99 es el máximo permitido para un edge estático (borde inferior a 100).
        var edge = Edge.FromStaticAnalysis(Proj, N1, N2, "SQL", "appsettings.json", 99);
        Assert.Equal(99, edge.Confidence);
    }

    // ---------- FromStaticAnalysis: casos que deben fallar ----------

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void FromStaticAnalysis_SinSource_LanzaDomainException(string? source)
    {
        Assert.Throws<DomainException>(() =>
            Edge.FromStaticAnalysis(Proj, N1, N2, "SQL", source!, 80));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void FromStaticAnalysis_SinType_LanzaDomainException(string? type)
    {
        Assert.Throws<DomainException>(() =>
            Edge.FromStaticAnalysis(Proj, N1, N2, type!, "src/api.ts", 80));
    }

    [Theory]
    [InlineData(100)]   // un edge estático nunca es un hecho confirmado
    [InlineData(101)]
    [InlineData(-1)]
    public void FromStaticAnalysis_ConConfianzaFueraDeRango_LanzaDomainException(int confidence)
    {
        Assert.Throws<DomainException>(() =>
            Edge.FromStaticAnalysis(Proj, N1, N2, "SQL", "appsettings.json", confidence));
    }

    // ---------- FromRuntimeTrace: casos válidos ----------

    [Fact]
    public void FromRuntimeTrace_ConDatosValidos_CreaEdgeRuntimeCon100()
    {
        // Act
        var edge = Edge.FromRuntimeTrace(Proj, N1, N2, "SQL", "trace:abc123");

        // Assert: invariante 3 — Confidence=100 sólo puede venir de runtime.
        Assert.Equal(EdgeSourceType.Runtime, edge.SourceType);
        Assert.Equal(100, edge.Confidence);
        Assert.Equal("trace:abc123", edge.Source);
    }

    // ---------- FromRuntimeTrace: casos que deben fallar ----------

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void FromRuntimeTrace_SinTraceSource_LanzaDomainException(string? traceSource)
    {
        Assert.Throws<DomainException>(() =>
            Edge.FromRuntimeTrace(Proj, N1, N2, "SQL", traceSource!));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void FromRuntimeTrace_SinType_LanzaDomainException(string? type)
    {
        Assert.Throws<DomainException>(() =>
            Edge.FromRuntimeTrace(Proj, N1, N2, type!, "trace:abc123"));
    }
}
