using MinhaGaragem.Application;

namespace MinhaGaragem.Api;

public static class MechanicEndpoints
{
    public static void MapMechanicEndpoints(this WebApplication app)
    {
        var mechanics = app.MapGroup("/api/mechanics").RequireAuthorization().WithTags("Mechanics");
        mechanics.MapGet(
            "/",
            (
                MechanicService service,
                CancellationToken ct,
                int page = 1,
                int pageSize = 20,
                string? search = null,
                string status = "active"
            ) => service.List(page, pageSize, search, status, ct)
        );
        mechanics.MapGet("/{id:guid}", (Guid id, MechanicService service, CancellationToken ct) =>
            service.Get(id, ct)
        );
        mechanics.MapPost(
            "/",
            async (MechanicInput input, MechanicService service, CancellationToken ct) =>
            {
                var mechanic = await service.Save(null, input, ct);
                return Results.Created($"/api/mechanics/{mechanic.Id}", mechanic);
            }
        );
        mechanics.MapPut(
            "/{id:guid}",
            (Guid id, MechanicInput input, MechanicService service, CancellationToken ct) =>
                service.Save(id, input, ct)
        );
        mechanics.MapDelete(
            "/{id:guid}",
            async (Guid id, MechanicService service, CancellationToken ct) =>
            {
                await service.SetArchived(id, true, ct);
                return Results.NoContent();
            }
        );
        mechanics.MapPost(
            "/{id:guid}/restore",
            (Guid id, MechanicService service, CancellationToken ct) =>
                service.SetArchived(id, false, ct)
        );
        mechanics.MapGet(
            "/{id:guid}/records",
            (
                Guid id,
                MechanicService service,
                CancellationToken ct,
                int page = 1,
                int pageSize = 10
            ) => service.Records(id, page, pageSize, ct)
        );
    }
}
