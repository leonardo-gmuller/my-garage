using MinhaGaragem.Domain;

namespace MinhaGaragem.Application.AI;

public sealed record AskHistoryInput(Guid VehicleId, string Question);

// Keys are resolved within the authorized vehicle; the model cannot supply IDs or query expressions.
public sealed record HistoryFilters
{
    public required string? ComponentKey { get; init; }
    public required string? SubcomponentKey { get; init; }
    public required int? Year { get; init; }
    public required string? SearchTerm { get; init; }
}

public sealed record HistoryToolArguments
{
    public required HistoryFilters Filters { get; init; }
}

public sealed record AssistantRecord(
    Guid Id,
    string Description,
    DateOnly Date,
    int? Mileage,
    decimal TotalCost,
    bool ClosedTotal,
    int PendingPaymentCount,
    string Url
);

public sealed record AssistantSchedule(ScheduleDto Schedule, string Url);

public sealed record HistoryAnswer(
    Guid VehicleId,
    string Kind,
    string Message,
    string Scope,
    int Count,
    decimal? Total,
    int PendingPaymentCount,
    IReadOnlyList<AssistantRecord> Records,
    IReadOnlyList<AssistantSchedule> Schedules,
    IReadOnlyList<string> Warnings,
    string PromptVersion = "history-v1"
);
