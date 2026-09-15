using Stratalens.Domain.Entities;
using Stratalens.Domain.Enums;
using Stratalens.Domain.Exceptions;

namespace Stratalens.Domain.Tests;

// Tests de las invariantes de Node.
public class NodeTests
{
    private static readonly Guid Proj = Guid.NewGuid();

    [Fact]
    public void Constructor_ConDatosValidos_CreaNode()
    {
        // Act
        var node = new Node(Proj, "AuthService", "AspNetCoreApi", NodeCategory.Application);

        // Assert
        Assert.Equal("AuthService", node.Name);
        Assert.Equal("AspNetCoreApi", node.Type);
        Assert.Equal(NodeCategory.Application, node.Category);
        Assert.NotEqual(Guid.Empty, node.Id);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_SinName_LanzaDomainException(string? name)
    {
        Assert.Throws<DomainException>(() =>
            new Node(Proj, name!, "PostgreSQL", NodeCategory.Database));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_SinType_LanzaDomainException(string? type)
    {
        Assert.Throws<DomainException>(() =>
            new Node(Proj, "MiNodo", type!, NodeCategory.Database));
    }

    // --- Jerarquía (T33): ParentNodeId. No hay test de "self-parent" porque es imposible
    // por construcción (el Id se genera dentro del ctor; nadie puede pasar un ParentNodeId
    // igual a un Id que aún no existe) — validarlo sería código muerto. Se prueba que el
    // campo viaja bien en sus dos formas: raíz (null) e hijo (id de otro nodo).

    [Fact]
    public void Constructor_SinParent_EsNodoRaiz()
    {
        var node = new Node(Proj, "Backend", "AspNetCore", NodeCategory.Application);

        Assert.Null(node.ParentNodeId);
    }

    [Fact]
    public void Constructor_ConParent_GuardaElIdDelPadre()
    {
        var backend = new Node(Proj, "Backend", "AspNetCore", NodeCategory.Application);

        var child = new Node(Proj, "ReportController", "Controller", NodeCategory.Code,
            metadata: null, parentNodeId: backend.Id);

        Assert.Equal(backend.Id, child.ParentNodeId);
    }
}
