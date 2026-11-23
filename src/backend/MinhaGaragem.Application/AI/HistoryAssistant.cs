using System.Diagnostics;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using MinhaGaragem.Application.AI.Prompts;

namespace MinhaGaragem.Application.AI;

public sealed class HistoryAssistant(
    GarageService garage,
    IGarageDb db,
    ICurrentUser user,
    IChatClient chat,
    MaintenanceAiOptions options,
    MaintenanceAiGate gate,
    HistoryQueries queries,
    ILogger<HistoryAssistant> logger
)
{
    public async Task<HistoryAnswer> Ask(AskHistoryInput input, CancellationToken ct)
    {
        var vehicle = await garage.Vehicle(input.VehicleId, ct);
        Rules.Text(input.Question, "Pergunta", options.MaxMessageLength, true);
        if (!options.Available)
            throw new MaintenanceAiException(AiFailure.Unavailable);
        // Unsupported periods require clarification instead of silently broadening the query.
        if (HistoryQueries.RequestedYear(input.Question, garage.Today.Year) == -1)
            return HistoryQueries.Clarify(vehicle.Id);
        using var lease = gate.Enter(user.Id);
        using var activity = MaintenanceAiTelemetry.Activities.StartActivity("history.ask");
        var start = Stopwatch.GetTimestamp();
        MaintenanceAiTelemetry.Requests.Add(1);
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TimeSpan.FromSeconds(options.TimeoutSeconds));
        try
        {
            HistoryAnswer? answer = null;
            async Task<HistoryAnswer> Run(
                string kind,
                HistoryFilters filters,
                CancellationToken cancellationToken
            ) =>
                answer = await queries.Run(
                    vehicle.Id,
                    kind,
                    filters,
                    input.Question,
                    cancellationToken
                );
            AIFunction[] tools =
            [
                AIFunctionFactory.Create(
                    (HistoryFilters filters, CancellationToken cancellationToken) =>
                        Run("latest", filters, cancellationToken),
                    "ultimo_servico",
                    "Consulta o último serviço registrado por componente ou descrição."
                ),
                AIFunctionFactory.Create(
                    (HistoryFilters filters, CancellationToken cancellationToken) =>
                        Run("spending", filters, cancellationToken),
                    "consultar_gastos",
                    "Calcula o total conhecido dos serviços do veículo, sem duplicar componentes."
                ),
                AIFunctionFactory.Create(
                    (HistoryFilters filters, CancellationToken cancellationToken) =>
                        Run("pending", filters, cancellationToken),
                    "consultar_pendencias",
                    "Consulta apenas os agendamentos não concluídos."
                ),
                AIFunctionFactory.Create(
                    () => answer = HistoryQueries.Clarify(vehicle.Id),
                    "esclarecer_pergunta",
                    "Solicita uma pergunta dentro do escopo e dos filtros suportados."
                ),
            ];
            var catalogue = await db
                .Components.AsNoTracking()
                .Include(c => c.Subcomponents)
                .Where(c => c.VehicleId == vehicle.Id)
                .ToArrayAsync(timeout.Token);
            var context = JsonSerializer.Serialize(
                new
                {
                    today = garage.Today,
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
                    question = input.Question,
                },
                MaintenanceAiJson.Options
            );
            var providerStart = Stopwatch.GetTimestamp();
            ChatResponse response;
            try
            {
                response = await chat.GetResponseAsync(
                    [new(ChatRole.System, HistoryPromptV1.System), new(ChatRole.User, context)],
                    new ChatOptions
                    {
                        ModelId = options.Model,
                        MaxOutputTokens = Math.Min(options.MaxOutputTokens, 1024),
                        Temperature = 0,
                        Tools = tools.Cast<AITool>().ToList(),
                        ToolMode = ChatToolMode.RequireAny,
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
            if (
                response.FinishReason is not null
                && response.FinishReason != ChatFinishReason.Stop
                && response.FinishReason != ChatFinishReason.ToolCalls
            )
                throw Invalid();
            var calls = response
                .Messages.SelectMany(m => m.Contents)
                .OfType<FunctionCallContent>()
                .ToArray();
            if (calls.Length != 1)
                throw Invalid();
            var call = calls[0];
            var tool = tools.SingleOrDefault(t => t.Name == call.Name) ?? throw Invalid();
            var raw = JsonSerializer.Serialize(
                call.Arguments ?? new Dictionary<string, object?>(),
                MaintenanceAiJson.Options
            );
            if (raw.Length > 2048)
                throw Invalid();
            if (call.Name == "esclarecer_pergunta")
            {
                if (call.Arguments?.Count > 0)
                    throw Invalid();
            }
            else
            {
                var args = JsonSerializer.Deserialize<HistoryToolArguments>(
                    raw,
                    MaintenanceAiJson.Options
                ) ?? throw Invalid();
                if (args.Filters is null)
                    throw Invalid();
            }
            // Exactly one validated, known read tool. No automatic loop and no result sent back to the provider.
            await tool.InvokeAsync(
                new AIFunctionArguments(call.Arguments ?? new Dictionary<string, object?>()),
                timeout.Token
            );
            return answer ?? throw Invalid();
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
        catch (JsonException)
        {
            Failure(AiFailure.InvalidResponse);
            throw Invalid();
        }
        catch (MaintenanceAiException error)
        {
            Failure(error.Failure);
            throw;
        }
        catch (Exception)
        {
            Failure(AiFailure.Unavailable);
            throw new MaintenanceAiException(AiFailure.Unavailable);
        }
        finally
        {
            MaintenanceAiTelemetry.Duration.Record(Stopwatch.GetElapsedTime(start).TotalSeconds);
        }
    }

    private static MaintenanceAiException Invalid() => new(AiFailure.InvalidResponse);

    private void Failure(AiFailure failure)
    {
        MaintenanceAiTelemetry.Failures.Add(
            1,
            new KeyValuePair<string, object?>("reason", failure.ToString())
        );
        Activity.Current?.SetStatus(ActivityStatusCode.Error, failure.ToString());
        logger.LogWarning(
            "History assistant failed: {Reason}; prompt {PromptVersion}",
            failure,
            HistoryPromptV1.Version
        );
    }
}
