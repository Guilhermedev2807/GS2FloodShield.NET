using FloodShield.Shelters.Api.Contracts;
using FloodShield.Shelters.Api.Domain;
using FloodShield.Shelters.Api.Infrastructure;
using FloodShield.Shelters.Api.Services;

namespace FloodShield.Shelters.Api.Endpoints;

public static class ShelterEndpoints
{
    public static IEndpointRouteBuilder MapShelterEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/shelters")
            .WithTags("Shelters")
            .RequireAuthorization()
            .RequireRateLimiting(Policies.RateLimitDefault);

        group.MapGet("/", List)
            .WithSummary("Lista abrigos (paginado, com filtros por status e busca)")
            .Produces<PagedResponse<ShelterResponse>>()
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status401Unauthorized);

        group.MapGet("/nearby", Nearby)
            .WithSummary("Abrigos mais próximos de uma coordenada (Haversine), ordenados por distância")
            .Produces<IReadOnlyList<NearbyShelterResponse>>()
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status401Unauthorized);

        group.MapGet("/{id:int}", GetById)
            .WithSummary("Detalha um abrigo")
            .Produces<ShelterResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status401Unauthorized);

        group.MapPost("/", Create)
            .RequireAuthorization(Policies.AdminOnly)
            .RequireIdempotency()
            .WithSummary("Cadastra um abrigo (Admin, idempotente)")
            .Produces<ShelterResponse>(StatusCodes.Status201Created)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

        group.MapPut("/{id:int}", Update)
            .RequireAuthorization(Policies.AdminOnly)
            .WithSummary("Atualiza um abrigo (Admin)")
            .Produces<ShelterResponse>()
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

        group.MapDelete("/{id:int}", Delete)
            .RequireAuthorization(Policies.AdminOnly)
            .WithSummary("Remove um abrigo (Admin)")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        group.MapPatch("/{id:int}/occupancy", SetOccupancy)
            .RequireAuthorization(Policies.AdminOnly)
            .RequireIdempotency()
            .WithSummary("Atualiza a ocupação atual (Admin, idempotente)")
            .Produces<ShelterResponse>()
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

        return app;
    }

    private static async Task<IResult> List(IShelterService service, CancellationToken ct,
        int page = 1, int pageSize = 20, ShelterStatus? status = null, string? search = null)
    {
        var errors = new Dictionary<string, string[]>();
        if (page < 1) errors["page"] = ["page deve ser >= 1."];
        if (pageSize is < 1 or > 100) errors["pageSize"] = ["pageSize deve estar entre 1 e 100."];
        if (errors.Count > 0) return Results.ValidationProblem(errors);

        return Results.Ok(await service.ListAsync(page, pageSize, status, search, ct));
    }

    private static async Task<IResult> Nearby(IShelterService service, CancellationToken ct,
        double lat, double lng, double radiusKm = 10, int limit = 20, bool onlyAvailable = true)
    {
        var errors = new Dictionary<string, string[]>();
        if (double.IsNaN(lat) || lat is < -90 or > 90) errors["lat"] = ["lat deve estar entre -90 e 90."];
        if (double.IsNaN(lng) || lng is < -180 or > 180) errors["lng"] = ["lng deve estar entre -180 e 180."];
        if (double.IsNaN(radiusKm) || radiusKm is <= 0 or > 200) errors["radiusKm"] = ["radiusKm deve estar entre 0 (exclusivo) e 200."];
        if (limit is < 1 or > 50) errors["limit"] = ["limit deve estar entre 1 e 50."];
        if (errors.Count > 0) return Results.ValidationProblem(errors);

        return Results.Ok(await service.NearbyAsync(lat, lng, radiusKm, limit, onlyAvailable, ct));
    }

    private static async Task<IResult> GetById(int id, IShelterService service, CancellationToken ct)
    {
        var shelter = await service.GetAsync(id, ct);
        return shelter is null ? NotFound(id) : Results.Ok(shelter);
    }

    private static async Task<IResult> Create(CreateShelterRequest request, IShelterService service, CancellationToken ct)
    {
        if (!RequestValidator.TryValidate(request, out var errors))
            return Results.ValidationProblem(errors);

        var created = await service.CreateAsync(request, ct);
        return Results.Created($"/shelters/{created.Id}", created);
    }

    private static async Task<IResult> Update(int id, UpdateShelterRequest request, IShelterService service, CancellationToken ct)
    {
        if (!RequestValidator.TryValidate(request, out var errors))
            return Results.ValidationProblem(errors);

        var updated = await service.UpdateAsync(id, request, ct);
        return updated is null ? NotFound(id) : Results.Ok(updated);
    }

    private static async Task<IResult> Delete(int id, IShelterService service, CancellationToken ct)
        => await service.DeleteAsync(id, ct) ? Results.NoContent() : NotFound(id);

    private static async Task<IResult> SetOccupancy(int id, OccupancyRequest request, IShelterService service, CancellationToken ct)
    {
        if (!RequestValidator.TryValidate(request, out var errors))
            return Results.ValidationProblem(errors);

        var updated = await service.SetOccupancyAsync(id, request.CurrentOccupancy, ct);
        return updated is null ? NotFound(id) : Results.Ok(updated);
    }

    private static IResult NotFound(int id)
        => Results.Problem(statusCode: StatusCodes.Status404NotFound, title: "Abrigo não encontrado",
            detail: $"Não existe abrigo com id {id}.");
}
