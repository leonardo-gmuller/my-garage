using MinhaGaragem.Application.AI;

namespace MinhaGaragem.Api;

public static class MaintenanceAiEndpoints
{
    public static void MapMaintenanceAiEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/ai/maintenance").WithTags("AI").RequireAuthorization();
        group.MapGet("/status", (MaintenanceInterpreter service) => service.Status());
        group
            .MapPost(
                "/interpret",
                async (
                    InterpretMaintenanceInput input,
                    MaintenanceInterpreter service,
                    CancellationToken ct
                ) => Results.Ok(await service.Interpret(input, ct))
            )
            .RequireRateLimiting("ai");
        app.MapPost(
                "/api/ai/assistant/ask",
                async (AskHistoryInput input, HistoryAssistant service, CancellationToken ct) =>
                    Results.Ok(await service.Ask(input, ct))
            )
            .WithTags("AI")
            .RequireAuthorization()
            .RequireRateLimiting("ai");
    }
}
