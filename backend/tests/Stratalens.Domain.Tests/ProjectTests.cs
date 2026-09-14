using Stratalens.Domain.Entities;
using Stratalens.Domain.Exceptions;

namespace Stratalens.Domain.Tests;

// Tests de las invariantes de Project.
public class ProjectTests
{
    [Fact]
    public void CreateManual_ConDatosValidos_CreaProjectSinRepositorio()
    {
        // Arrange
        var owner = Guid.NewGuid();

        // Act
        var project = Project.CreateManual(owner, "Mi App");

        // Assert
        Assert.Equal(owner, project.OwnerUserId);
        Assert.Equal("Mi App", project.Name);
        Assert.NotEqual(Guid.Empty, project.Id);
        Assert.Null(project.RepositoryOwner);
        Assert.Null(project.RepositoryName);
        Assert.Null(project.RepositoryReference);
    }

    [Fact]
    public void CreateManual_GuardaCreatedAtEnUtc()
    {
        // CreatedAt debe estar en UTC para evitar bugs de zona horaria.
        var project = Project.CreateManual(Guid.NewGuid(), "Mi App");
        Assert.Equal(DateTimeKind.Utc, project.CreatedAt.Kind);
    }

    [Fact]
    public void CreateManual_ConOwnerVacio_LanzaDomainException()
    {
        // Un proyecto sin dueño rompería el aislamiento entre usuarios.
        Assert.Throws<DomainException>(() =>
            Project.CreateManual(Guid.Empty, "Mi App"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void CreateManual_SinName_LanzaDomainException(string? name)
    {
        Assert.Throws<DomainException>(() =>
            Project.CreateManual(Guid.NewGuid(), name!));
    }

    [Fact]
    public void CreateFromRepository_ConDatosValidos_CreaProjectConLosTresCampos()
    {
        // Arrange
        var owner = Guid.NewGuid();

        // Act
        var project = Project.CreateFromRepository(owner, "carlos", "mi-repo", "main");

        // Assert: Name se deriva del repo, no se pide aparte.
        Assert.Equal(owner, project.OwnerUserId);
        Assert.Equal("mi-repo", project.Name);
        Assert.Equal("carlos", project.RepositoryOwner);
        Assert.Equal("mi-repo", project.RepositoryName);
        Assert.Equal("main", project.RepositoryReference);
    }

    [Fact]
    public void CreateFromRepository_ConOwnerVacio_LanzaDomainException()
    {
        Assert.Throws<DomainException>(() =>
            Project.CreateFromRepository(Guid.Empty, "carlos", "mi-repo", "main"));
    }

    // All-or-nothing: si se elige el factory de repositorio, los tres campos son
    // obligatorios. No hay un cuarto caso "dos de tres" porque no hay forma pública
    // de construir un Project que no pase por uno de los dos factories completos.
    [Theory]
    [InlineData(null, "mi-repo", "main")]
    [InlineData("carlos", null, "main")]
    [InlineData("carlos", "mi-repo", null)]
    [InlineData("", "mi-repo", "main")]
    public void CreateFromRepository_ConAlgunCampoFaltante_LanzaDomainException(
        string? repositoryOwner, string? repositoryName, string? repositoryReference)
    {
        Assert.Throws<DomainException>(() =>
            Project.CreateFromRepository(Guid.NewGuid(), repositoryOwner!, repositoryName!, repositoryReference!));
    }
}
