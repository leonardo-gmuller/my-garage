using System.Diagnostics;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using MinhaGaragem.Application.AI.Prompts;

namespace MinhaGaragem.Application.AI;

public sealed class MaintenanceInterpreter(
    GarageService garage,
    IGarageDb db,
    ICurrentUser user,
    IChatClient chat,
    MaintenanceAiOptions options,
    MaintenanceAiGate gate,
    ILogger<MaintenanceInterpreter> logger
)
{
    public MaintenanceAiStatus Status() =>
        new(
            options.Available,
            options.MaxMessageLength,
            options.Available
                ? null
                : "IA indisponível nesta instalação. Você pode registrar o serviço manualmente."
        );

    public async Task<MaintenanceAiDraft> Interpret(
        InterpretMaintenanceInput input,
        CancellationToken ct
    )
    {
        // Authorize before quota accounting or contacting the provider. No model-generated IDs are accepted.
        var vehicle = await garage.Vehicle(input.VehicleId, ct);
        Rules.Text(input.Message, "Descrição da manutenção", options.MaxMessageLength, true);
        if (!options.Available)
            throw new MaintenanceAiException(AiFailure.Unavailable);
        var catalogue = await db
            .Components.AsNoTracking()
            .Include(c => c.Subcomponents)
            .Where(c => c.VehicleId == vehicle.Id)
            .ToListAsync(ct);
        using var lease = gate.Enter(user.Id);
        using var activity = MaintenanceAiTelemetry.Activities.StartActivity(
            "maintenance.interpret"
        );
        var start = Stopwatch.GetTimestamp();
        MaintenanceAiTelemetry.Requests.Add(1);
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TimeSpan.FromSeconds(options.TimeoutSeconds));
        try
        {
            var context = JsonSerializer.Serialize(
                new
                {
                    vehicle = new
                    {
                        vehicle.Manufacturer,
                        vehicle.Model,
                        vehicle.ModelYear,
                        vehicle.Version,
                    },
                    catalogue = catalogue.Select(c => new
                    {
                        c.Key,
                        c.Name,
                        subcomponents = c.Subcomponents.Select(s => new { s.Key, s.Name }),
                    }),
                },
                MaintenanceAiJson.Options
            );
            var providerStart = Stopwatch.GetTimestamp();
            ChatResponse response;
            try
            {
                response = await chat.GetResponseAsync(
                    [
                        new(ChatRole.System, MaintenancePromptV1.System),
                        new(
                            ChatRole.User,
                            JsonSerializer.Serialize(
                                new
                                {
                                    vehicleContext = JsonSerializer.Deserialize<JsonElement>(
                                        context
                                    ),
                                    maintenanceText = input.Message,
                                },
                                MaintenanceAiJson.Options
                            )
                        ),
                    ],
                    new ChatOptions
                    {
                        ModelId = options.Model,
                        MaxOutputTokens = options.MaxOutputTokens,
                        Temperature = 0,
                        ResponseFormat = ChatResponseFormat.ForJsonSchema<MaintenanceExtraction>(
                            MaintenanceAiJson.Options
                        ),
                    },
                    timeout.Token
                );
            }
            finally
            {
                MaintenanceAiTelemetry.ProviderDuration.Record(
                    Stopwatch.GetElapsedTime(providerStart).TotalSeconds
                );
            }
            if (response.Usage?.InputTokenCount is long inputTokens)
                MaintenanceAiTelemetry.InputTokens.Add(inputTokens);
            if (response.Usage?.OutputTokenCount is long outputTokens)
                MaintenanceAiTelemetry.OutputTokens.Add(outputTokens);
            if (response.FinishReason is not null && response.FinishReason != ChatFinishReason.Stop)
                throw new MaintenanceAiException(AiFailure.InvalidResponse);
            return MaintenanceDraftValidator.Parse(
                response.Text,
                input.Message,
                vehicle,
                catalogue,
                garage.Today
            );
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            Failure(AiFailure.Timeout);
            throw new MaintenanceAiException(AiFailure.Timeout);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (MaintenanceAiException error)
        {
            Failure(error.Failure);
            throw;
        }
        catch (Exception)
        {
            // Provider exceptions can contain request data or credentials. Never log exception bodies.
            Failure(AiFailure.Unavailable);
            throw new MaintenanceAiException(AiFailure.Unavailable);
        }
        finally
        {
            MaintenanceAiTelemetry.Duration.Record(Stopwatch.GetElapsedTime(start).TotalSeconds);
        }
    }

    private void Failure(AiFailure failure)
    {
        MaintenanceAiTelemetry.Failures.Add(
            1,
            new KeyValuePair<string, object?>("reason", failure.ToString())
        );
        Activity.Current?.SetStatus(ActivityStatusCode.Error, failure.ToString());
        logger.LogWarning(
            "Maintenance interpretation failed: {Reason}; prompt {PromptVersion}",
            failure,
            MaintenancePromptV1.Version
        );
    }
}
