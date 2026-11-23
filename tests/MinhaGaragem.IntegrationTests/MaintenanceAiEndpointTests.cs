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
using MinhaGaragem.TestSupport;

namespace MinhaGaragem.IntegrationTests;

[Collection("api")]
[Trait("Category", "AI")]
public sealed class MaintenanceAiEndpointTests(ApiFixture fixture)
{
    private const string Message =
        "Troquei velas em 01/01/2026. Paguei 850 reais. O carro estava com 125 mil km.";
    private const string Output = """
        {"description":"Troca de velas","category":"Engine","serviceType":"Replacement","mileage":125000,"mileageEvidence":"125 mil km","totalCost":850,"totalCostEvidence":"850 reais","serviceDate":"2026-01-01","serviceDateEvidence":"01/01/2026","workshop":null,"notes":null,"components":[{"key":"engine","subcomponentKeys":["spark-plugs"],"evidence":"velas"}],"items":[{"name":"velas","quantity":null,"evidence":"velas","quantityEvidence":null}],"unmatchedComponents":[],"warnings":[]}
        """;
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() },
    };

    private static FakeChatClient Fake(string json = Output) =>
        new()
        {
            Respond = _ =>
                Task.FromResult(
                    new ChatResponse(new ChatMessage(ChatRole.Assistant, json))
                    {
                        FinishReason = ChatFinishReason.Stop,
                        Usage = new() { InputTokenCount = 100, OutputTokenCount = 200 },
                    }
                ),
        };

    private WebApplicationFactory<Program> Host(
        FakeChatClient fake,
        MaintenanceAiOptions? options = null
    ) =>
        fixture.Factory.WithWebHostBuilder(b =>
            b.ConfigureTestServices(services =>
            {
                services.RemoveAll<IChatClient>();
                services.AddSingleton<IChatClient>(fake);
                services.RemoveAll<MaintenanceAiOptions>();
                services.AddSingleton(options ?? new(Available: true, RequestsPerMinute: 100));
            })
        );

    private static async Task Csrf(HttpClient client)
    {
        client.DefaultRequestHeaders.Remove("X-CSRF-TOKEN");
        var token = await client.GetFromJsonAsync<JsonElement>("/api/auth/csrf");
        client.DefaultRequestHeaders.Add("X-CSRF-TOKEN", token.GetProperty("token").GetString());
    }

    private async Task<HttpClient> Login(WebApplicationFactory<Program> host)
    {
        var email = await fixture.CreateAccount("GarageTest1234");
        var client = host.CreateClient(
            new() { BaseAddress = new("https://localhost"), AllowAutoRedirect = false }
        );
        await Csrf(client);
        (
            await client.PostAsJsonAsync(
                "/api/auth/login",
                new { email, password = "GarageTest1234" }
            )
        ).EnsureSuccessStatusCode();
        await Csrf(client);
        return client;
    }

    private static async Task<VehicleDto> Vehicle(HttpClient client) =>
        (await client.GetFromJsonAsync<Page<VehicleDto>>("/api/vehicles", Json))!.Items[0];

    private static Task<HttpResponseMessage> Interpret(
        HttpClient client,
        Guid vehicle,
        string? message = null
    ) =>
        client.PostAsJsonAsync(
            "/api/ai/maintenance/interpret",
            new { vehicleId = vehicle, message = message ?? Message }
        );

    private static async Task EmptyHistory(HttpClient client, Guid vehicle) =>
        Assert.Equal(
            0,
            (
                await client.GetFromJsonAsync<Page<RecordDto>>(
                    $"/api/vehicles/{vehicle}/records",
                    Json
                )
            )!.Total
        );

    [Fact]
    public async Task InterpretationDoesNotWriteUntilTheNormalRecordEndpointIsExplicitlyConfirmed()
    {
        var fake = Fake();
        await using var host = Host(fake);
        using var client = await Login(host);
        var vehicle = await Vehicle(client);
        var result = await Interpret(client, vehicle.Id);
        result.EnsureSuccessStatusCode();
        var draft = (await result.Content.ReadFromJsonAsync<MaintenanceAiDraft>(Json))!;
        Assert.Equal(850, draft.TotalCost);
        Assert.Equal(vehicle.Id, draft.VehicleId);
        Assert.Null(draft.Items[0].Quantity);
        Assert.Equal(1, fake.Calls);
        Assert.IsType<ChatResponseFormatJson>(fake.Options!.ResponseFormat);
        Assert.Equal(2048, fake.Options.MaxOutputTokens);
        Assert.Null(fake.Options.Tools);
        var context = fake.Messages[1].Text;
        Assert.DoesNotContain(vehicle.Id.ToString(), context);
        Assert.DoesNotContain("userId", context);
        Assert.DoesNotContain("plate", context);
        Assert.DoesNotContain("mileage", context);
        await EmptyHistory(client, vehicle.Id);
        Assert.Null((await Vehicle(client)).Mileage);
        var input = new RecordInput(
            draft.ServiceDate!.Value,
            draft.Category!.Value,
            draft.ServiceType!.Value,
            draft.Description,
            draft.Mileage,
            0,
            null,
            null,
            draft.Components.Select(c => c.Id).ToList(),
            [new("velas", 4, 0)],
            draft.Components.SelectMany(c => c.Subcomponents.Select(s => s.Id)).ToList(),
            draft.Components[0].Id,
            PackageTotal: draft.TotalCost
        );
        (
            await client.PostAsJsonAsync($"/api/vehicles/{vehicle.Id}/records", input, Json)
        ).EnsureSuccessStatusCode();
        var history = (
            await client.GetFromJsonAsync<Page<RecordDto>>(
                $"/api/vehicles/{vehicle.Id}/records",
                Json
            )
        )!;
        Assert.Equal(1, history.Total);
        Assert.Equal(850, history.Items[0].TotalCost);
        Assert.Null(history.Items[0].Items[0].UnitCost);
    }

    [Fact]
    public async Task AuthenticationAndVehicleOwnershipAreCheckedBeforeAnyProviderCall()
    {
        var fake = Fake();
        await using var host = Host(fake);
        using var anonymous = host.CreateClient();
        Assert.Equal(
            HttpStatusCode.Unauthorized,
            (await anonymous.GetAsync("/api/ai/maintenance/status")).StatusCode
        );
        Assert.Equal(
            HttpStatusCode.Unauthorized,
            (await Interpret(anonymous, Guid.NewGuid())).StatusCode
        );
        using var owner = await Login(host);
        var vehicle = await Vehicle(owner);
        using var other = await Login(host);
        Assert.Equal(HttpStatusCode.NotFound, (await Interpret(other, vehicle.Id)).StatusCode);
        Assert.Equal(0, fake.Calls);
        await EmptyHistory(owner, vehicle.Id);
    }

    [Fact]
    public async Task CsrfAndInputBoundsPreventProviderCalls()
    {
        var fake = Fake();
        await using var host = Host(fake);
        using var client = await Login(host);
        var vehicle = await Vehicle(client);
        client.DefaultRequestHeaders.Remove("X-CSRF-TOKEN");
        Assert.Equal(HttpStatusCode.BadRequest, (await Interpret(client, vehicle.Id)).StatusCode);
        await Csrf(client);
        Assert.Equal(
            HttpStatusCode.BadRequest,
            (await Interpret(client, vehicle.Id, new string('a', 4001))).StatusCode
        );
        Assert.Equal(
            HttpStatusCode.BadRequest,
            (await Interpret(client, vehicle.Id, " ")).StatusCode
        );
        Assert.Equal(0, fake.Calls);
    }

    [Fact]
    public async Task DisabledAiLeavesTheManualApplicationAvailable()
    {
        var fake = Fake();
        await using var host = Host(fake, new(Available: false));
        using var client = await Login(host);
        var vehicle = await Vehicle(client);
        Assert.False(
            (
                await client.GetFromJsonAsync<MaintenanceAiStatus>("/api/ai/maintenance/status")
            )!.Available
        );
        Assert.Equal(
            HttpStatusCode.ServiceUnavailable,
            (await Interpret(client, vehicle.Id)).StatusCode
        );
        (await client.GetAsync($"/api/vehicles/{vehicle.Id}/components")).EnsureSuccessStatusCode();
        Assert.Equal(0, fake.Calls);
    }

    [Theory]
    [InlineData("not JSON")]
    [InlineData("{}")]
    [InlineData("unknown-component")]
    public async Task InvalidProviderOutputReturnsProblemDetailsAndNeverWrites(string response)
    {
        var fake = Fake(
            response == "unknown-component"
                ? Output.Replace("\"engine\"", "\"unknown-component\"")
                : response
        );
        await using var host = Host(fake);
        using var client = await Login(host);
        var vehicle = await Vehicle(client);
        var result = await Interpret(client, vehicle.Id);
        Assert.Equal(HttpStatusCode.BadGateway, result.StatusCode);
        Assert.Contains("application/problem+json", result.Content.Headers.ContentType!.MediaType!);
        Assert.Contains("traceId", await result.Content.ReadAsStringAsync());
        await EmptyHistory(client, vehicle.Id);
        Assert.Equal(1, fake.Calls);
    }

    [Fact]
    public async Task ProviderFailureDoesNotExposeSensitiveExceptionContentOrRetry()
    {
        var fake = Fake();
        fake.Respond = _ => throw new InvalidOperationException("sensitive-provider-content");
        await using var host = Host(fake);
        using var client = await Login(host);
        var vehicle = await Vehicle(client);
        var result = await Interpret(client, vehicle.Id);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, result.StatusCode);
        Assert.DoesNotContain(
            "sensitive-provider-content",
            await result.Content.ReadAsStringAsync()
        );
        Assert.Equal(1, fake.Calls);
        await EmptyHistory(client, vehicle.Id);
    }

    [Fact]
    public async Task TimeoutCancelsTheProviderAndNeverRetries()
    {
        var cancelled = false;
        var fake = Fake();
        fake.Respond = async ct =>
        {
            try
            {
                await Task.Delay(Timeout.Infinite, ct);
                return new ChatResponse([]);
            }
            finally
            {
                cancelled = ct.IsCancellationRequested;
            }
        };
        await using var host = Host(fake, new(Available: true, TimeoutSeconds: 1));
        using var client = await Login(host);
        var vehicle = await Vehicle(client);
        Assert.Equal(
            HttpStatusCode.GatewayTimeout,
            (await Interpret(client, vehicle.Id)).StatusCode
        );
        Assert.True(cancelled);
        Assert.Equal(1, fake.Calls);
        await EmptyHistory(client, vehicle.Id);
    }

    [Fact]
    public async Task RateLimitsArePerUserAndDoNotPreventManualRequests()
    {
        var fake = Fake();
        await using var host = Host(fake, new(Available: true, RequestsPerMinute: 2));
        using var client = await Login(host);
        var vehicle = await Vehicle(client);
        (await Interpret(client, vehicle.Id)).EnsureSuccessStatusCode();
        (await Interpret(client, vehicle.Id)).EnsureSuccessStatusCode();
        var limited = await Interpret(client, vehicle.Id);
        Assert.Equal(HttpStatusCode.TooManyRequests, limited.StatusCode);
        Assert.NotNull(limited.Headers.RetryAfter);
        Assert.Equal(2, fake.Calls);
        (await client.GetAsync($"/api/vehicles/{vehicle.Id}/components")).EnsureSuccessStatusCode();
        using var other = await Login(host);
        var otherVehicle = await Vehicle(other);
        (await Interpret(other, otherVehicle.Id)).EnsureSuccessStatusCode();
        Assert.Equal(3, fake.Calls);
    }

    [Fact]
    public async Task ProviderQuotaAndTruncatedResponsesAreReportedWithoutSaving()
    {
        var fake = Fake();
        fake.Respond = _ => throw new MaintenanceAiException(AiFailure.RateLimited);
        await using var host = Host(fake);
        using var client = await Login(host);
        var vehicle = await Vehicle(client);
        Assert.Equal(
            HttpStatusCode.TooManyRequests,
            (await Interpret(client, vehicle.Id)).StatusCode
        );
        fake.Respond = _ =>
            Task.FromResult(
                new ChatResponse(new ChatMessage(ChatRole.Assistant, Output))
                {
                    FinishReason = ChatFinishReason.Length,
                }
            );
        Assert.Equal(HttpStatusCode.BadGateway, (await Interpret(client, vehicle.Id)).StatusCode);
        await EmptyHistory(client, vehicle.Id);
    }
}
