using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Stratalens.Api.Auth;
using Stratalens.Api.Contracts;
using Stratalens.Application.Abstractions;
using Stratalens.Application.Exceptions;
using Stratalens.Application.Models;
using Stratalens.Application.UseCases;
using Stratalens.Domain.Entities;

namespace Stratalens.Api.Controllers;

// CRUD mínimo de proyectos. [Authorize] exige sesión válida en todos los endpoints.
// La lógica de negocio (crear el Project, invariantes, ownership del grafo) vive en el
// dominio/Application; el controller solo traduce HTTP ↔ dominio.
[ApiController]
[Authorize]
[Route("api/v1/projects")]
public class ProjectsController : ControllerBase
{
    private readonly IValidator<CreateProjectRequest> _validator;
    private readonly IValidator<CreateProjectFromRepositoryRequest> _fromRepositoryValidator;
    private readonly CreateProjectUseCase _createProject;
    private readonly CreateProjectFromRepositoryUseCase _createProjectFromRepository;
    private readonly GetProjectUseCase _getProject;
    private readonly GetProjectGraphUseCase _getGraph;
    private readonly GetProjectTracesUseCase _getTraces;
    private readonly ReanalyzeProjectUseCase _reanalyze;
    private readonly ListProjectsUseCase _listProjects;

    public ProjectsController(
        IValidator<CreateProjectRequest> validator,
        IValidator<CreateProjectFromRepositoryRequest> fromRepositoryValidator,
        CreateProjectUseCase createProject,
        CreateProjectFromRepositoryUseCase createProjectFromRepository,
        GetProjectUseCase getProject,
        GetProjectGraphUseCase getGraph,
        GetProjectTracesUseCase getTraces,
        ReanalyzeProjectUseCase reanalyze,
        ListProjectsUseCase listProjects)
    {
        _validator = validator;
        _fromRepositoryValidator = fromRepositoryValidator;
        _createProject = createProject;
        _createProjectFromRepository = createProjectFromRepository;
        _getProject = getProject;
        _getGraph = getGraph;
        _getTraces = getTraces;
        _reanalyze = reanalyze;
        _listProjects = listProjects;
    }

    // Dashboard de "mis proyectos" (T28). No confundir con GetById: este es el listado.
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<ProjectDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ListMine(CancellationToken ct)
    {
        var userId = User.GetAppUserId()!.Value; // [Authorize] garantiza que existe
        var projects = await _listProjects.ExecuteAsync(userId, ct);
        return Ok(projects.Select(ToDto).ToList());
    }

    [HttpPost]
    [ProducesResponseType(typeof(CreateProjectResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create([FromBody] CreateProjectRequest request, CancellationToken ct)
    {
        var validation = await _validator.ValidateAsync(request, ct);
        if (!validation.IsValid)
            return BadRequest(validation.ToDictionary());

        var userId = User.GetAppUserId()!.Value; // [Authorize] garantiza que existe

        // El use case crea el proyecto Y genera su clave de ingesta (devuelta una vez).
        var result = await _createProject.ExecuteAsync(userId, request.Name, ct);
        var response = new CreateProjectResponse(
            result.Project.Id, result.Project.Name, result.Project.CreatedAt, result.IngestKey);

        return CreatedAtAction(nameof(GetById), new { id = result.Project.Id }, response);
    }

    // Crea el proyecto desde un repo elegido de GET /github/repositories (T25) y lo
    // analiza de inmediato (síncrono, T26). Distinto de Create: ese es manual (T1).
    [HttpPost("from-repository")]
    [ProducesResponseType(typeof(CreateProjectResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CreateFromRepository(
        [FromBody] CreateProjectFromRepositoryRequest request, CancellationToken ct)
    {
        var validation = await _fromRepositoryValidator.ValidateAsync(request, ct);
        if (!validation.IsValid)
            return BadRequest(validation.ToDictionary());

        var userId = User.GetAppUserId()!.Value; // [Authorize] garantiza que existe

        try
        {
            var result = await _createProjectFromRepository.ExecuteAsync(userId, request.Owner, request.Name, ct);
            var response = new CreateProjectResponse(
                result.Project.Id, result.Project.Name, result.Project.CreatedAt, result.IngestKey);

            return CreatedAtAction(nameof(GetById), new { id = result.Project.Id }, response);
        }
        catch (NotFoundException)
        {
            // El repo no existe o no es del usuario (ListRepositoriesAsync ya filtra por owner).
            return NotFound();
        }
        catch (ConflictException)
        {
            return Conflict();
        }
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(ProjectDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var userId = User.GetAppUserId()!.Value; // [Authorize] garantiza que existe

        // El ownership check vive en el use case (Application); aquí solo traducimos la
        // NotFoundException a un 404 (sin revelar la existencia de proyectos ajenos).
        try
        {
            var project = await _getProject.ExecuteAsync(id, userId, ct);
            return Ok(ToDto(project));
        }
        catch (NotFoundException)
        {
            return NotFound();
        }
    }

    // Devuelve el grafo de arquitectura del proyecto. El ownership check vive en el use
    // case (Application): si el proyecto no existe o es de otro usuario, lanza
    // NotFoundException y aquí la traducimos a 404 (sin revelar la existencia).
    [HttpGet("{id:guid}/graph")]
    [ProducesResponseType(typeof(GraphResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetGraph(Guid id, CancellationToken ct)
    {
        var userId = User.GetAppUserId()!.Value; // [Authorize] garantiza que existe

        try
        {
            var graph = await _getGraph.ExecuteAsync(id, userId, ct);
            return Ok(ToDto(graph));
        }
        catch (NotFoundException)
        {
            return NotFound();
        }
    }

    // Re-analiza un proyecto ya conectado a un repo (T27): repite el pipeline de T10 de
    // forma síncrona con el contenido ACTUAL del repo. 404 si es ajeno, no existe, o es
    // un proyecto manual sin repo conectado (mismo NotFoundException, misma respuesta).
    [HttpPost("{id:guid}/analyze")]
    [ProducesResponseType(typeof(GraphResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Reanalyze(Guid id, CancellationToken ct)
    {
        var userId = User.GetAppUserId()!.Value; // [Authorize] garantiza que existe

        try
        {
            var graph = await _reanalyze.ExecuteAsync(id, userId, ct);
            return Ok(ToDto(graph));
        }
        catch (NotFoundException)
        {
            return NotFound();
        }
    }

    // Devuelve los traces persistidos del proyecto (T17). Mismo patrón de ownership que
    // GetGraph: el use case lanza NotFoundException → 404 si no es del usuario.
    [HttpGet("{id:guid}/traces")]
    [ProducesResponseType(typeof(IReadOnlyList<TraceDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetTraces(Guid id, CancellationToken ct)
    {
        var userId = User.GetAppUserId()!.Value; // [Authorize] garantiza que existe

        try
        {
            var traces = await _getTraces.ExecuteAsync(id, userId, ct);
            return Ok(traces.Select(ToDto).ToList());
        }
        catch (NotFoundException)
        {
            return NotFound();
        }
    }

    private static ProjectDto ToDto(Project project) =>
        new(project.Id, project.Name, project.CreatedAt, project.RepositoryOwner, project.RepositoryName);

    // La duración total del trace es la del span raíz: abarca toda la request.
    private static TraceDto ToDto(Trace trace)
    {
        var root = trace.Spans.FirstOrDefault(s => s.IsRoot);
        return new TraceDto(
            trace.TraceId,
            root?.DurationMs ?? 0,
            trace.Spans
                .Select(s => new SpanDto(
                    s.SpanId, s.ParentSpanId, s.SourceNode, s.TargetNode,
                    s.Operation, s.StartedAt, s.DurationMs, s.Status))
                .ToList());
    }

    private static GraphResponse ToDto(ProjectGraph graph) =>
        new(
            graph.Nodes
                .Select(n => new GraphNodeDto(n.Id, n.Name, n.Type, n.Category.ToString(), n.Metadata, n.ParentNodeId))
                .ToList(),
            graph.Edges
                .Select(e => new GraphEdgeDto(
                    e.Id, e.SourceNodeId, e.TargetNodeId, e.Type, e.Source, e.Confidence, e.SourceType.ToString()))
                .ToList());
}
