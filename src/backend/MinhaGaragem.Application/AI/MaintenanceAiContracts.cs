using System.Text.Json;
using System.Text.Json.Serialization;
using MinhaGaragem.Domain;

namespace MinhaGaragem.Application.AI;

public sealed record InterpretMaintenanceInput(Guid VehicleId, string Message);

public sealed record MaintenanceAiStatus(bool Available, int MaxMessageLength, string? Reason);

public sealed record MaintenanceAiOptions(
    bool Available = false,
    string Model = "gemini-3.5-flash-lite",
    int TimeoutSeconds = 30,
    int MaxMessageLength = 4000,
    int MaxOutputTokens = 2048,
    int RequestsPerMinute = 3,
    int RequestsPerUserPerDay = 20,
    int RequestsPerDay = 100
);

public enum AiFailure
{
    Unavailable,
    RateLimited,
    Timeout,
    InvalidResponse,
}

public sealed class MaintenanceAiException(AiFailure failure)
    : Exception("Falha na interpretação de manutenção.")
{
    public AiFailure Failure { get; } = failure;
}

public sealed record AiDraftItem(string Name, int? Quantity);

public sealed record AiDraftComponent(
    Guid Id,
    string Key,
    string Name,
    IReadOnlyList<SubcomponentDto> Subcomponents
);

public sealed record MaintenanceAiDraft(
    Guid VehicleId,
    string Description,
    MaintenanceCategory? Category,
    ServiceType? ServiceType,
    int? Mileage,
    decimal? TotalCost,
    DateOnly? ServiceDate,
    string? Workshop,
    string? Notes,
    IReadOnlyList<AiDraftComponent> Components,
    IReadOnlyList<AiDraftItem> Items,
    IReadOnlyList<string> MissingFields,
    IReadOnlyList<string> Warnings,
    string PromptVersion
);

// Model-facing DTOs deliberately contain catalogue keys, never database IDs.
public sealed record ExtractedComponent
{
    public required string Key { get; init; }
    public required List<string> SubcomponentKeys { get; init; }
    public required string Evidence { get; init; }
}

public sealed record ExtractedItem
{
    public required string Name { get; init; }
    public required int? Quantity { get; init; }
    public required string Evidence { get; init; }
    public required string? QuantityEvidence { get; init; }
}

public sealed record MaintenanceExtraction
{
    public required string Description { get; init; }
    public required MaintenanceCategory? Category { get; init; }
    public required ServiceType? ServiceType { get; init; }
    public required int? Mileage { get; init; }
    public required string? MileageEvidence { get; init; }
    public required decimal? TotalCost { get; init; }
    public required string? TotalCostEvidence { get; init; }
    public required DateOnly? ServiceDate { get; init; }
    public required string? ServiceDateEvidence { get; init; }
    public required string? Workshop { get; init; }
    public required string? Notes { get; init; }
    public required List<ExtractedComponent> Components { get; init; }
    public required List<ExtractedItem> Items { get; init; }
    public required List<string> UnmatchedComponents { get; init; }
    public required List<string> Warnings { get; init; }
}

public static class MaintenanceAiJson
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        MaxDepth = 16,
        Converters =
        {
            new JsonStringEnumConverter<MaintenanceCategory>(allowIntegerValues: false),
            new JsonStringEnumConverter<ServiceType>(allowIntegerValues: false),
        },
    };
}
