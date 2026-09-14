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
}
