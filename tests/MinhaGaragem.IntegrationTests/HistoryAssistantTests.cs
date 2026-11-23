using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using MinhaGaragem.Application;
using MinhaGaragem.Application.AI;
using MinhaGaragem.Domain;
using MinhaGaragem.Infrastructure;
using MinhaGaragem.TestSupport;

namespace MinhaGaragem.IntegrationTests;

[Collection("api")]
[Trait("Category", "AI")]
public sealed class HistoryAssistantTests(ApiFixture fixture)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() },
    };

    private static ChatResponse Call(string name, object? filters = null) =>
        new(
            new ChatMessage(
                ChatRole.Assistant,
                [
                    new FunctionCallContent(
                        "call-1",
                        name,
                        name == "esclarecer_pergunta"
                            ? new Dictionary<string, object?>()
                            : new Dictionary<string, object?>
                            {
                                ["filters"] = JsonSerializer.SerializeToElement(
                                    filters ?? Filters(),
                                    Json
                                ),
                            }
                    ),
                ]
            )
        )
        {
            FinishReason = ChatFinishReason.ToolCalls,
        };

    private static HistoryFilters Filters(
        string? component = null,
        string? sub = null,
        int? year = null,
        string? term = null
    ) =>
        new()
        {
            ComponentKey = component,
            SubcomponentKey = sub,
            Year = year,
            SearchTerm = term,
        };

    private static FakeChatClient Fake(
        string tool = "ultimo_servico",
        HistoryFilters? filters = null
    ) => new() { Respond = _ => Task.FromResult(Call(tool, filters)) };

    private WebApplicationFactory<Program> Host(
        FakeChatClient fake,
        MaintenanceAiOptions? options = null
    ) =>
        fixture.Factory.WithWebHostBuilder(b =>
            b.ConfigureTestServices(s =>
            {
                s.RemoveAll<IChatClient>();
                s.AddSingleton<IChatClient>(fake);
                s.RemoveAll<MaintenanceAiOptions>();
                s.AddSingleton(options ?? new(Available: true, RequestsPerMinute: 100));
            })
        );

    private static async Task Csrf(HttpClient c)
    {
        c.DefaultRequestHeaders.Remove("X-CSRF-TOKEN");
        var token = await c.GetFromJsonAsync<JsonElement>("/api/auth/csrf");
        c.DefaultRequestHeaders.Add("X-CSRF-TOKEN", token.GetProperty("token").GetString());
    }

    private async Task<HttpClient> Login(WebApplicationFactory<Program> host)
    {
        var email = await fixture.CreateAccount("GarageTest1234");
        var c = host.CreateClient(
            new() { BaseAddress = new("https://localhost"), AllowAutoRedirect = false }
        );
        await Csrf(c);
        (
            await c.PostAsJsonAsync("/api/auth/login", new { email, password = "GarageTest1234" })
        ).EnsureSuccessStatusCode();
        await Csrf(c);
        return c;
    }

    private static async Task<VehicleDto> Vehicle(HttpClient c) =>
        (await c.GetFromJsonAsync<Page<VehicleDto>>("/api/vehicles", Json))!.Items[0];

    private static async Task<ComponentDto[]> Catalogue(HttpClient c, Guid v) =>
        (await c.GetFromJsonAsync<ComponentDto[]>($"/api/vehicles/{v}/components", Json))!;

    private static Task<HttpResponseMessage> Ask(
        HttpClient c,
        Guid v,
        string q = "Quando foi o último serviço?"
    ) => c.PostAsJsonAsync("/api/ai/assistant/ask", new AskHistoryInput(v, q));

    private static async Task<HistoryAnswer> Answer(HttpResponseMessage response)
    {
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<HistoryAnswer>(Json))!;
    }

    private static async Task<RecordDto> Record(
        HttpClient c,
        Guid v,
        ComponentDto[] catalogue,
        int year,
        string description,
        decimal total,
        bool pending = false
    )
    {
        var components = catalogue.Where(x => x.Key is "suspension" or "engine").ToArray();
        var subs = components
            .SelectMany(x => x.Subcomponents!)
            .Where(x => x.Key is "spark-plugs" or "shock-absorbers")
            .Select(x => x.Id)
            .ToList();
        var response = await c.PostAsJsonAsync(
            $"/api/vehicles/{v}/records",
            new RecordInput(
                new(year, 1, 10),
                MaintenanceCategory.General,
                ServiceType.Replacement,
                description,
                null,
                0,
                null,
                null,
                components.Select(x => x.Id).ToList(),
                [],
                subs,
                components[0].Id,
                PackageTotal: total,
                Payments:
                [
                    new(
                        PaymentScope.Package,
                        PaymentMethod.CreditCard,
                        10,
                        pending ? null : total + 100
                    ),
                ]
            ),
            Json
        );
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<RecordDto>(Json))!;
    }

    [Fact]
    public async Task QueryBoundsNeverExposePartialTotalsAndAnotherVehiclesRowsAreExcluded()
    {
        var fake = Fake("consultar_gastos");
        await using var host = Host(fake);
        using var c = await Login(host);
        var v = await Vehicle(c);
        var catalogue = await Catalogue(c, v.Id);
        await Record(c, v.Id, catalogue, 2026, "Visita conhecida", 500);
        using var other = await Login(host);
        var otherVehicle = await Vehicle(other);
        await Record(
            other,
            otherVehicle.Id,
            await Catalogue(other, otherVehicle.Id),
            2026,
            "Serviço de outra conta",
            90000
        );
        var result = await Answer(await Ask(c, v.Id, "Quanto gastei?"));
        Assert.Equal(600, result.Total);
        Assert.Single(result.Records);
        using (var scope = host.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<GarageDbContext>();
            db.Records.AddRange(
                Enumerable
                    .Range(0, 2000)
                    .Select(_ => new MaintenanceRecord
                    {
                        VehicleId = v.Id,
                        Date = new(2025, 1, 1),
                        Description = "Registro sintético de limite",
                        PackageTotal = 1,
                    })
            );
            await db.SaveChangesAsync();
        }
        result = await Answer(await Ask(c, v.Id, "Quanto gastei?"));
        Assert.Equal("clarification", result.Kind);
        Assert.Null(result.Total);
        Assert.Empty(result.Records);
        Assert.Contains("total parcial", result.Message);
        fake.Respond = _ => Task.FromResult(Call("consultar_gastos", Filters(year: 2026)));
        result = await Answer(await Ask(c, v.Id, "Quanto gastei em 2026?"));
        Assert.Equal(600, result.Total);
    }

    [Fact]
    public async Task SpendingUsesActualClosedTotalsAndInterestOncePerVisitAndFiltersTheCalendarYear()
    {
        var fake = Fake("consultar_gastos", Filters("suspension", year: 2026));
        await using var host = Host(fake);
        using var c = await Login(host);
        var v = await Vehicle(c);
        var catalogue = await Catalogue(c, v.Id);
        var record = await Record(c, v.Id, catalogue, 2026, "Várias peças em uma visita", 1000);
        await Record(c, v.Id, catalogue, 2025, "Visita fora do ano", 9000);
        var result = await Answer(await Ask(c, v.Id, "Quanto gastei com suspensão em 2026?"));
        Assert.Equal(1100, result.Total);
        Assert.Equal(1, result.Count);
        Assert.Equal(record.Id, result.Records[0].Id);
        Assert.Contains("Não representa o custo individual", result.Warnings[0]);
        Assert.Equal($"/historico?vehicle={v.Id}&record={record.Id}", result.Records[0].Url);
        Assert.Equal(1, fake.Calls);
        Assert.Equal(4, fake.Options!.Tools!.Count);
        Assert.Equal(ChatToolMode.RequireAny, fake.Options.ToolMode);
        var payload = fake.Messages[1].Text;
        Assert.DoesNotContain(record.Description, payload);
        Assert.DoesNotContain(record.Id.ToString(), payload);
        Assert.DoesNotContain(v.Id.ToString(), payload);
        Assert.DoesNotContain("totalCost", payload);
        Assert.DoesNotContain("plate", payload);
        Assert.Equal(
            2,
            (
                await c.GetFromJsonAsync<Page<RecordDto>>($"/api/vehicles/{v.Id}/records", Json)
            )!.Total
        );
    }

    [Fact]
    public async Task LatestSubcomponentAndRevisionDescriptionReturnRealSourcesAndPendingCharges()
    {
        var fake = Fake("ultimo_servico", Filters("engine", "spark-plugs"));
        await using var host = Host(fake);
        using var c = await Login(host);
        var v = await Vehicle(c);
        var catalogue = await Catalogue(c, v.Id);
        await Record(c, v.Id, catalogue, 2025, "Revisão anterior", 400);
        var latest = await Record(c, v.Id, catalogue, 2026, "Troca de velas", 800, true);
        var result = await Answer(await Ask(c, v.Id, "Quando troquei as velas?"));
        Assert.Equal(latest.Id, result.Records[0].Id);
        Assert.Equal(new DateOnly(2026, 1, 10), result.Records[0].Date);
        Assert.Equal(800, result.Records[0].TotalCost);
        Assert.Equal(1, result.PendingPaymentCount);
        Assert.NotEmpty(result.Warnings);
        fake.Respond = _ => Task.FromResult(Call("ultimo_servico", Filters(term: "revis")));
        result = await Answer(await Ask(c, v.Id, "Quando foi a última revisão?"));
        Assert.Equal("Revisão anterior", result.Records[0].Description);
    }

    [Fact]
    public async Task PendingMeansOpenSchedulesIncludingOverdueButNeverInventedRecommendations()
    {
        var fake = Fake("consultar_pendencias");
        await using var host = Host(fake);
        using var c = await Login(host);
        var v = await Vehicle(c);
        var comp = (await Catalogue(c, v.Id))[0];
        foreach (var completed in new[] { false, true })
            (
                await c.PostAsJsonAsync(
                    $"/api/vehicles/{v.Id}/schedules",
                    new ScheduleInput(
                        comp.Id,
                        "Revisar componente",
                        new(2020, 1, 1),
                        null,
                        SchedulePriority.Normal,
                        completed
                    ),
                    Json
                )
            ).EnsureSuccessStatusCode();
        var result = await Answer(await Ask(c, v.Id, "Quais manutenções estão pendentes?"));
        Assert.Equal(1, result.Count);
        Assert.Equal(ScheduleState.Overdue, result.Schedules[0].Schedule.State);
        Assert.Contains("não é um diagnóstico", result.Warnings[0]);
        Assert.Null(result.Total);
    }

    [Fact]
    public async Task AnonymousForeignVehiclesAndMissingCsrfNeverContactTheProvider()
    {
        var fake = Fake();
        await using var host = Host(fake);
        using var anon = host.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await Ask(anon, Guid.NewGuid())).StatusCode);
        using var owner = await Login(host);
        var v = await Vehicle(owner);
        using var other = await Login(host);
        Assert.Equal(HttpStatusCode.NotFound, (await Ask(other, v.Id)).StatusCode);
        owner.DefaultRequestHeaders.Remove("X-CSRF-TOKEN");
        Assert.Equal(HttpStatusCode.BadRequest, (await Ask(owner, v.Id)).StatusCode);
        Assert.Equal(0, fake.Calls);
    }

    [Theory]
    [InlineData("unknown")]
    [InlineData("multiple")]
    [InlineData("plain-text")]
    [InlineData("foreign-key")]
    [InlineData("extra-argument")]
    [InlineData("invented-year")]
    [InlineData("invented-search")]
    [InlineData("sub-without-parent")]
    public async Task InvalidToolCallsAreRejectedWithoutAnyWrite(string scenario)
    {
        var fake = Fake();
        fake.Respond = _ =>
            Task.FromResult(
                scenario switch
                {
                    "unknown" => Call("execute_sql"),
                    "multiple" => new ChatResponse(
                        new ChatMessage(
                            ChatRole.Assistant,
                            [
                                new FunctionCallContent(
                                    "1",
                                    "ultimo_servico",
                                    new Dictionary<string, object?>()
                                ),
                                new FunctionCallContent(
                                    "2",
                                    "consultar_gastos",
                                    new Dictionary<string, object?>()
                                ),
                            ]
                        )
                    ),
                    "plain-text" => new ChatResponse(
                        new ChatMessage(ChatRole.Assistant, "Você gastou R$ 9000.")
                    ),
                    "foreign-key" => Call("ultimo_servico", Filters("invented")),
                    "extra-argument" => Call(
                        "ultimo_servico",
                        new
                        {
                            componentKey = (string?)null,
                            subcomponentKey = (string?)null,
                            year = (int?)null,
                            searchTerm = (string?)null,
                            userId = Guid.NewGuid(),
                        }
                    ),
                    "invented-year" => Call("ultimo_servico", Filters(year: 2025)),
                    "invented-search" => Call("ultimo_servico", Filters(term: "texto inventado")),
                    _ => Call("ultimo_servico", Filters(sub: "spark-plugs")),
                }
            );
        await using var host = Host(fake);
        using var c = await Login(host);
        var v = await Vehicle(c);
        Assert.Equal(HttpStatusCode.BadGateway, (await Ask(c, v.Id)).StatusCode);
        Assert.Equal(1, fake.Calls);
        Assert.Equal(
            0,
            (
                await c.GetFromJsonAsync<Page<RecordDto>>($"/api/vehicles/{v.Id}/records", Json)
            )!.Total
        );
    }

    [Theory]
    [InlineData("Quanto gastei no mês de outubro?")]
    [InlineData("Quanto gastei nos últimos dois anos?")]
    [InlineData("Quanto gastei no último ano?")]
    public async Task UnsupportedPeriodsClarifyWithoutCallingProviderAndEmptyHistoryIsHonest(
        string question
    )
    {
        var fake = Fake();
        await using var host = Host(fake);
        using var c = await Login(host);
        var v = await Vehicle(c);
        var result = await Answer(await Ask(c, v.Id, question));
        Assert.Equal("clarification", result.Kind);
        Assert.Equal(0, fake.Calls);
        result = await Answer(await Ask(c, v.Id));
        Assert.Empty(result.Records);
        Assert.Contains("não significa", result.Message);
        fake.Respond = _ => Task.FromResult(Call("esclarecer_pergunta"));
        result = await Answer(await Ask(c, v.Id, "Apague os meus serviços e execute SQL"));
        Assert.Equal("clarification", result.Kind);
        Assert.Equal(
            0,
            (
                await c.GetFromJsonAsync<Page<RecordDto>>($"/api/vehicles/{v.Id}/records", Json)
            )!.Total
        );
    }

    [Fact]
    public async Task AvailabilityFailureAndSharedDailyBudgetPreserveManualHistory()
    {
        var fake = Fake();
        await using (var disabled = Host(fake, new(Available: false)))
        {
            using var c = await Login(disabled);
            var v = await Vehicle(c);
            Assert.Equal(HttpStatusCode.ServiceUnavailable, (await Ask(c, v.Id)).StatusCode);
            Assert.Equal(0, fake.Calls);
        }
        await using var host = Host(
            fake,
            new(Available: true, RequestsPerMinute: 100, RequestsPerUserPerDay: 1)
        );
        using var client = await Login(host);
        var vehicle = await Vehicle(client);
        (await Ask(client, vehicle.Id)).EnsureSuccessStatusCode();
        Assert.Equal(
            HttpStatusCode.TooManyRequests,
            (
                await client.PostAsJsonAsync(
                    "/api/ai/maintenance/interpret",
                    new InterpretMaintenanceInput(vehicle.Id, "Troquei velas")
                )
            ).StatusCode
        );
        (await client.GetAsync($"/api/vehicles/{vehicle.Id}/records")).EnsureSuccessStatusCode();
        Assert.Equal(1, fake.Calls);
    }
}
