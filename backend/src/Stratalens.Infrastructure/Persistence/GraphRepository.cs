using Microsoft.EntityFrameworkCore;
using Stratalens.Application.Abstractions;
using Stratalens.Application.Models;
using Stratalens.Domain.Entities;

namespace Stratalens.Infrastructure.Persistence;

// Implementación de IGraphRepository con EF Core + PostgreSQL.
// Este es "el lado derecho del diagrama": Application define el contrato,
// esta clase lo cumple. Application/Domain no saben que existe.
public class GraphRepository : IGraphRepository
{
    private readonly StratalensDbContext _db;

    public GraphRepository(StratalensDbContext db)
    {
        _db = db;
    }

    public Task<Project?> GetProjectAsync(Guid projectId, CancellationToken ct = default)
        => _db.Projects.FirstOrDefaultAsync(p => p.Id == projectId, ct);

    public async Task<IReadOnlyList<Project>> GetProjectsByOwnerAsync(Guid ownerUserId, CancellationToken ct = default) =>
        await _db.Projects.AsNoTracking()
            .Where(p => p.OwnerUserId == ownerUserId)
            .OrderByDescending(p => p.CreatedAt)
            .ToListAsync(ct);

    public async Task AddProjectAsync(Project project, CancellationToken ct = default)
    {
        _db.Projects.Add(project);
        await _db.SaveChangesAsync(ct);
    }

    public async Task SaveGraphAsync(Guid projectId, AnalysisResult graph, CancellationToken ct = default)
    {
        // "Idempotente por proyecto": reemplaza el grafo anterior por el nuevo.
        // Todo dentro de una transacción: si algo falla, no perdemos el grafo previo
        // dejando la mitad borrada (atomicidad).
        await using var tx = await _db.Database.BeginTransactionAsync(ct);

        await _db.Edges.Where(e => e.ProjectId == projectId).ExecuteDeleteAsync(ct);
        await _db.Nodes.Where(n => n.ProjectId == projectId).ExecuteDeleteAsync(ct);

        _db.Nodes.AddRange(graph.Nodes);
        _db.Edges.AddRange(graph.Edges);
        await _db.SaveChangesAsync(ct);

        await tx.CommitAsync(ct);
    }

    public async Task<ProjectGraph> GetGraphAsync(Guid projectId, CancellationToken ct = default)
    {
        // AsNoTracking: solo lectura, no vamos a modificar estas entidades → más rápido.
        var nodes = await _db.Nodes.AsNoTracking()
            .Where(n => n.ProjectId == projectId).ToListAsync(ct);
        var edges = await _db.Edges.AsNoTracking()
            .Where(e => e.ProjectId == projectId).ToListAsync(ct);

        return new ProjectGraph(nodes, edges);
    }

    public async Task SetIngestKeyHashAsync(Guid projectId, string ingestKeyHash, CancellationToken ct = default)
    {
        var project = await _db.Projects.FirstOrDefaultAsync(p => p.Id == projectId, ct)
            ?? throw new InvalidOperationException($"El proyecto {projectId} no existe.");

        // Escribimos la shadow property vía el change tracker (no es propiedad del CLR),
        // igual que UserRepository hace con el token cifrado.
        _db.Entry(project).Property("IngestKeyHash").CurrentValue = ingestKeyHash;
        await _db.SaveChangesAsync(ct);
    }

    public async Task<Guid?> GetProjectIdByIngestKeyHashAsync(string ingestKeyHash, CancellationToken ct = default)
    {
        // EF.Property<> permite filtrar por una shadow property que no existe en el CLR.
        var project = await _db.Projects.AsNoTracking()
            .FirstOrDefaultAsync(p => EF.Property<string>(p, "IngestKeyHash") == ingestKeyHash, ct);

        return project?.Id;
    }

    public async Task<IReadOnlyList<Trace>> GetTracesAsync(Guid projectId, CancellationToken ct = default)
    {
        // Include(Spans): cargamos el agregado completo (raíz + spans) en una sola query.
        return await _db.Traces.AsNoTracking()
            .Include(t => t.Spans)
            .Where(t => t.ProjectId == projectId)
            .ToListAsync(ct);
    }

    public async Task AddEdgeAsync(Edge edge, CancellationToken ct = default)
    {
        _db.Edges.Add(edge);
        await _db.SaveChangesAsync(ct);
    }

    public async Task RemoveEdgeAsync(Guid edgeId, CancellationToken ct = default)
    {
        // ExecuteDelete: borrado directo por Id sin rehidratar la entidad (no la necesitamos).
        await _db.Edges.Where(e => e.Id == edgeId).ExecuteDeleteAsync(ct);
    }

    public Task<bool> ExistsProjectForRepositoryAsync(
        Guid ownerUserId, string repositoryOwner, string repositoryName, CancellationToken ct = default) =>
        _db.Projects.AnyAsync(p =>
            p.OwnerUserId == ownerUserId &&
            p.RepositoryOwner == repositoryOwner &&
            p.RepositoryName == repositoryName, ct);
}
