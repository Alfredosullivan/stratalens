using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Stratalens.Api.Contracts;
using Stratalens.Application.Abstractions;
using Stratalens.Application.Models;

namespace Stratalens.Api.Tests;

// T25: GET /api/v1/github/repositories. Sustituimos IProviderConnector por un stub (sin
// red real, mismo espíritu que StubConnector en Infrastructure.Tests) vía
// WithWebHostBuilder — así no tocamos CustomWebApplicationFactory, compartida por el
// resto de los tests que no necesitan hablar con GitHub.
public class GitHubRepositoriesTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public GitHubRepositoriesTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    private HttpClient ClientWithStubConnector(Guid userId, IReadOnlyList<RepositorySummary> repos)
    {
        var client = _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll(typeof(IProviderConnector));
                services.AddScoped<IProviderConnector>(_ => new StubProviderConnector(repos));
            });
        }).CreateClient();

        client.DefaultRequestHeaders.Add(TestAuthHandler.UserHeader, userId.ToString());
        return client;
    }

    [Fact]
    public async Task ListRepositories_Autenticado_DevuelveLosDelConnector()
    {
        // Arrange
        var repos = new List<RepositorySummary>
        {
            new("carlos", "stratalens", "main", false),
            new("carlos", "otro-repo", "develop", true)
        };
        var client = ClientWithStubConnector(Guid.NewGuid(), repos);

        // Act
        var resp = await client.GetAsync("/api/v1/github/repositories");

        // Assert
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var body = await resp.Content.ReadFromJsonAsync<List<RepositorySummaryDto>>();
        Assert.Equal(2, body!.Count);
        Assert.Equal("stratalens", body[0].Name);
        Assert.False(body[0].IsPrivate);
        Assert.True(body[1].IsPrivate);
    }

    [Fact]
    public async Task ListRepositories_SinAutenticar_Devuelve401()
    {
        // Sin stub: [Authorize] rechaza antes de que el controller llegue a pedir el
        // connector real, así que no hace falta reemplazarlo para este caso.
        var client = _factory.CreateClient();
        var resp = await client.GetAsync("/api/v1/github/repositories");
        Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
    }

    // Stub mínimo: T25 solo ejercita ListRepositoriesAsync desde la Api.
    private sealed class StubProviderConnector : IProviderConnector
    {
        private readonly IReadOnlyList<RepositorySummary> _repos;
        public StubProviderConnector(IReadOnlyList<RepositorySummary> repos) => _repos = repos;

        public Task<IReadOnlyList<RepositorySummary>> ListRepositoriesAsync(CancellationToken ct = default) =>
            Task.FromResult(_repos);

        public Task<IReadOnlyList<RepositoryFile>> GetFileTreeAsync(RepositoryReference repo, CancellationToken ct = default) =>
            throw new NotSupportedException("No usado en este test.");

        public Task<string> GetFileContentAsync(RepositoryReference repo, string path, CancellationToken ct = default) =>
            throw new NotSupportedException("No usado en este test.");
    }
}
