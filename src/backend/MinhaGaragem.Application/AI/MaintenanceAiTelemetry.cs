using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace MinhaGaragem.Application.AI;

public static class MaintenanceAiTelemetry
{
    public const string Name = "MinhaGaragem.AI";
    public static readonly ActivitySource Activities = new(Name);
    private static readonly Meter Meter = new(Name);
    public static readonly Counter<long> Requests = Meter.CreateCounter<long>("garage.ai.requests");
    public static readonly Counter<long> Failures = Meter.CreateCounter<long>("garage.ai.failures");
    public static readonly Histogram<double> Duration = Meter.CreateHistogram<double>(
        "garage.ai.duration",
        "s"
    );
    public static readonly Histogram<double> ProviderDuration = Meter.CreateHistogram<double>(
        "garage.ai.provider.duration",
        "s"
    );
    public static readonly Counter<long> InputTokens = Meter.CreateCounter<long>(
        "garage.ai.tokens.input"
    );
    public static readonly Counter<long> OutputTokens = Meter.CreateCounter<long>(
        "garage.ai.tokens.output"
    );
}
