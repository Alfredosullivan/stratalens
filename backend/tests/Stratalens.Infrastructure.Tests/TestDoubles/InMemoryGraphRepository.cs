using Stratalens.Application.Abstractions;
using Stratalens.Application.Models;
using Stratalens.Domain.Entities;

namespace Stratalens.Infrastructure.Tests.TestDoubles;

// Fake de IGraphRepository en memoria, para tests de pipeline que no necesitan Postgres
// real (a diferencia de GraphRepositoryTests, que sí prueban la implementación EF Core).
// Compartido entre AnalyzeRepositoryPipelineTests (T10) y NodeExpressPipelineTests: ambos
// solo ejercitan Project + el grafo, así que duplicarlo por archivo era puro boilerplate.
internal sealed class InMemoryGraphRepository : IGraphRepository
{
    private readonly Dictionary<Guid, Project> _projects = new();
    private AnalysisResult _graph = new(new List<Node>(), new List<Edge>());

    public Task AddProjectAsync(Project project, CancellationToken ct = default)
    {
        _projects[project.Id] = project;
        return Task.CompletedTask;
    }

    public Task<Project?> GetProjectAsync(Guid projectId, CancellationToken ct = default) =>
        Task.FromResult(_projects.GetValueOrDefault(projectId));

    public Task SaveGraphAsync(Guid projectId, AnalysisResult graph, CancellationToken ct = default)
    {
        _graph = graph;
        return Task.CompletedTask;
    }

    public Task<ProjectGraph> GetGraphAsync(Guid projectId, CancellationToken ct = default) =>
        Task.FromResult(new ProjectGraph(_graph.Nodes, _graph.Edges));

    // No relevantes para los pipelines de análisis; los fakes no los ejercitan.
    public Task SetIngestKeyHashAsync(Guid projectId, string ingestKeyHash, CancellationToken ct = default) =>
        throw new NotSupportedException();

    public Task<Guid?> GetProjectIdByIngestKeyHashAsync(string ingestKeyHash, CancellationToken ct = default) =>
        throw new NotSupportedException();

    public Task<IReadOnlyList<Trace>> GetTracesAsync(Guid projectId, CancellationToken ct = default) =>
        throw new NotSupportedException();

    public Task AddEdgeAsync(Edge edge, CancellationToken ct = default) =>
        throw new NotSupportedException();

    public Task RemoveEdgeAsync(Guid edgeId, CancellationToken ct = default) =>
        throw new NotSupportedException();

    public Task<bool> ExistsProjectForRepositoryAsync(
        Guid ownerUserId, string repositoryOwner, string repositoryName, CancellationToken ct = default) =>
        throw new NotSupportedException();

    public Task<IReadOnlyList<Project>> GetProjectsByOwnerAsync(Guid ownerUserId, CancellationToken ct = default) =>
        throw new NotSupportedException();
}
