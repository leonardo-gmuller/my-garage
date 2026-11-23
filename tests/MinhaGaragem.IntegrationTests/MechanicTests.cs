using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using MinhaGaragem.Application;
using MinhaGaragem.Domain;

namespace MinhaGaragem.IntegrationTests;

[Collection("api")]
public sealed class MechanicTests(ApiFixture fixture)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    private HttpClient Client() => fixture.Factory.CreateClient(new() { HandleCookies = true });

    private static async Task Csrf(HttpClient client)
    {
        client.DefaultRequestHeaders.Remove("X-CSRF-TOKEN");
        var data = await client.GetFromJsonAsync<JsonElement>("/api/auth/csrf");
        client.DefaultRequestHeaders.Add("X-CSRF-TOKEN", data.GetProperty("token").GetString());
    }

    private async Task<HttpClient> SignInNewAccount()
    {
        var email = await fixture.CreateAccount("GarageTest1234");
        var client = Client();
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

    private static async Task<ComponentDto[]> Components(HttpClient client, Guid id) =>
        (await client.GetFromJsonAsync<ComponentDto[]>($"/api/vehicles/{id}/components", Json))!;

    private static async Task<MechanicDto> Create(
        HttpClient client,
        string name = "  João Pereira  "
    )
    {
        var response = await client.PostAsJsonAsync(
            "/api/mechanics",
            new MechanicInput(
                name,
                "Oficina Central",
                "Suspensão",
                "+55 (11) 99999-8888",
                "joao@example.test",
                "Rua da Oficina, 10",
                "Indicação de teste"
            ),
            Json
        );
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<MechanicDto>(Json))!;
    }

    private static RecordInput Service(Guid component, Guid? mechanic = null) =>
        new(
            new(2026, 1, 1),
            MaintenanceCategory.Engine,
            ServiceType.Replacement,
            "Revisão de teste",
            null,
            80,
            null,
            null,
            [component],
            [new("Filtro", 1, 20)],
            MechanicId: mechanic
        );

    private static async Task<RecordDto> Save(
        HttpClient c,
        Guid v,
        RecordInput input,
        Guid? id = null
    )
    {
        var response = id.HasValue
            ? await c.PutAsJsonAsync($"/api/vehicles/{v}/records/{id}", input, Json)
            : await c.PostAsJsonAsync($"/api/vehicles/{v}/records", input, Json);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<RecordDto>(Json))!;
    }

    [Fact]
    public async Task DirectoryIsPrivateAndMutationsRequireCsrf()
    {
        using var anonymous = Client();
        Assert.Equal(
            HttpStatusCode.Unauthorized,
            (await anonymous.GetAsync("/api/mechanics")).StatusCode
        );
        using var owner = await SignInNewAccount();
        var mechanic = await Create(owner);
        Assert.Equal("João Pereira", mechanic.Name);
        Assert.Equal(0, mechanic.ServiceCount);
        Assert.Null(mechanic.LastServiceDate);
        using var other = await SignInNewAccount();
        Assert.Empty(
            (await other.GetFromJsonAsync<Page<MechanicDto>>("/api/mechanics", Json))!.Items
        );
        Assert.Equal(
            HttpStatusCode.NotFound,
            (await other.GetAsync($"/api/mechanics/{mechanic.Id}")).StatusCode
        );
        Assert.Equal(
            HttpStatusCode.NotFound,
            (await other.GetAsync($"/api/mechanics/{mechanic.Id}/records")).StatusCode
        );
        Assert.Equal(
            HttpStatusCode.NotFound,
            (
                await other.PutAsJsonAsync(
                    $"/api/mechanics/{mechanic.Id}",
                    new MechanicInput("Outro nome"),
                    Json
                )
            ).StatusCode
        );
        Assert.Equal(
            HttpStatusCode.NotFound,
            (await other.DeleteAsync($"/api/mechanics/{mechanic.Id}")).StatusCode
        );
        Assert.Equal(
            HttpStatusCode.NotFound,
            (
                await other.PostAsJsonAsync($"/api/mechanics/{mechanic.Id}/restore", new { })
            ).StatusCode
        );
        owner.DefaultRequestHeaders.Remove("X-CSRF-TOKEN");
        Assert.Equal(
            HttpStatusCode.BadRequest,
            (
                await owner.PostAsJsonAsync("/api/mechanics", new MechanicInput("Sem token"))
            ).StatusCode
        );
        Assert.Equal(
            HttpStatusCode.BadRequest,
            (await owner.DeleteAsync($"/api/mechanics/{mechanic.Id}")).StatusCode
        );
    }

    [Fact]
    public async Task ContactsWorkWithoutVehiclesAndSearchIsPaginated()
    {
        using var c = await SignInNewAccount();
        var vehicle = await Vehicle(c);
        (await c.DeleteAsync($"/api/vehicles/{vehicle.Id}")).EnsureSuccessStatusCode();
        var first = await Create(c);
        await Create(c, "Ana Mecânica");
        var search = (
            await c.GetFromJsonAsync<Page<MechanicDto>>(
                "/api/mechanics?search=PEREIRA&pageSize=1",
                Json
            )
        )!;
        Assert.Equal(1, search.Total);
        Assert.Equal(first.Id, Assert.Single(search.Items).Id);
        var page = (
            await c.GetFromJsonAsync<Page<MechanicDto>>("/api/mechanics?pageSize=1&page=2", Json)
        )!;
        Assert.Equal(2, page.Total);
        Assert.Single(page.Items);
        Assert.Equal(
            HttpStatusCode.BadRequest,
            (await c.GetAsync("/api/mechanics?status=invalid")).StatusCode
        );
        Assert.Equal(
            HttpStatusCode.BadRequest,
            (await c.GetAsync("/api/mechanics?pageSize=1000")).StatusCode
        );
    }

    [Fact]
    public async Task LinksAreOptionalAndCannotCrossAccounts()
    {
        using var owner = await SignInNewAccount();
        using var other = await SignInNewAccount();
        var v = await Vehicle(owner);
        var component = (await Components(owner, v.Id))[0];
        var foreign = await Create(other);
        Assert.Equal(
            HttpStatusCode.BadRequest,
            (
                await owner.PostAsJsonAsync(
                    $"/api/vehicles/{v.Id}/records",
                    Service(component.Id, foreign.Id),
                    Json
                )
            ).StatusCode
        );
        var legacy = await Save(
            owner,
            v.Id,
            Service(component.Id) with
            {
                Workshop = "Oficina informada livremente",
            }
        );
        Assert.Null(legacy.MechanicId);
        Assert.Null(legacy.MechanicName);
        Assert.Equal("Oficina informada livremente", legacy.Workshop);
        var contact = await Create(owner);
        var record = await Save(owner, v.Id, Service(component.Id, contact.Id));
        Assert.Equal(contact.Id, record.MechanicId);
        Assert.Equal("João Pereira", record.MechanicName);
        Assert.Equal("Oficina Central", record.Workshop);
        Assert.Equal(100, record.TotalCost);
        var detached = await Save(
            owner,
            v.Id,
            Service(component.Id) with
            {
                Workshop = record.Workshop,
            },
            record.Id
        );
        Assert.Null(detached.MechanicId);
        Assert.Null(detached.MechanicName);
        Assert.Equal("Oficina Central", detached.Workshop);
    }

    [Fact]
    public async Task ArchivePreservesSnapshotsAndAllowsEditingExistingService()
    {
        using var c = await SignInNewAccount();
        var v = await Vehicle(c);
        var component = (await Components(c, v.Id))[0];
        var m = await Create(c);
        var input = Service(component.Id, m.Id);
        var record = await Save(c, v.Id, input);
        (
            await c.PutAsJsonAsync(
                $"/api/mechanics/{m.Id}",
                new MechanicInput("Nome atualizado", "Outra oficina"),
                Json
            )
        ).EnsureSuccessStatusCode();
        (await c.DeleteAsync($"/api/mechanics/{m.Id}")).EnsureSuccessStatusCode();
        Assert.Empty((await c.GetFromJsonAsync<Page<MechanicDto>>("/api/mechanics", Json))!.Items);
        Assert.True(
            Assert
                .Single(
                    (
                        await c.GetFromJsonAsync<Page<MechanicDto>>(
                            "/api/mechanics?status=archived",
                            Json
                        )
                    )!.Items
                )
                .IsArchived
        );
        Assert.Equal(
            HttpStatusCode.BadRequest,
            (await c.PostAsJsonAsync($"/api/vehicles/{v.Id}/records", input, Json)).StatusCode
        );
        var edited = await Save(
            c,
            v.Id,
            input with
            {
                Description = "Descrição corrigida",
            },
            record.Id
        );
        Assert.Equal("João Pereira", edited.MechanicName);
        Assert.Equal("Oficina Central", edited.Workshop);
        var history = (
            await c.GetFromJsonAsync<Page<RecordDto>>($"/api/mechanics/{m.Id}/records", Json)
        )!;
        Assert.Equal(record.Id, Assert.Single(history.Items).Id);
        Assert.Equal(1, history.Total);
        (
            await c.PostAsJsonAsync($"/api/mechanics/{m.Id}/restore", new { })
        ).EnsureSuccessStatusCode();
        var restored = Assert.Single(
            (await c.GetFromJsonAsync<Page<MechanicDto>>("/api/mechanics", Json))!.Items
        );
        Assert.False(restored.IsArchived);
        Assert.Equal(1, restored.ServiceCount);
        var newRecord = await Save(c, v.Id, input);
        Assert.Equal("Nome atualizado", newRecord.MechanicName);
        Assert.Equal("Outra oficina", newRecord.Workshop);
    }

    [Fact]
    public async Task ContactHistorySpansOwnedVehiclesAndCountsServicesOnce()
    {
        using var c = await SignInNewAccount();
        var v = await Vehicle(c);
        var m = await Create(c);
        var components = await Components(c, v.Id);
        await Save(
            c,
            v.Id,
            Service(components[0].Id, m.Id) with
            {
                ComponentIds = [components[0].Id, components[1].Id],
            }
        );
        var response = await c.PostAsJsonAsync(
            "/api/vehicles",
            new VehicleInput("Ford", "Ka", 2010, 2010, null, null, null, null, null),
            Json
        );
        response.EnsureSuccessStatusCode();
        var second = (await response.Content.ReadFromJsonAsync<VehicleDto>(Json))!;
        await Save(c, second.Id, Service((await Components(c, second.Id))[0].Id, m.Id));
        var contact = (await c.GetFromJsonAsync<MechanicDto>($"/api/mechanics/{m.Id}", Json))!;
        Assert.Equal(2, contact.ServiceCount);
        Assert.Equal(new DateOnly(2026, 1, 1), contact.LastServiceDate);
        var history = (
            await c.GetFromJsonAsync<Page<RecordDto>>(
                $"/api/mechanics/{m.Id}/records?pageSize=1",
                Json
            )
        )!;
        Assert.Equal(2, history.Total);
        Assert.Single(history.Items);
        var all = (
            await c.GetFromJsonAsync<Page<RecordDto>>($"/api/mechanics/{m.Id}/records", Json)
        )!;
        Assert.Equal(200, all.Items.Sum(r => r.TotalCost));
        (await c.DeleteAsync($"/api/vehicles/{v.Id}")).EnsureSuccessStatusCode();
        Assert.Equal(
            1,
            (await c.GetFromJsonAsync<MechanicDto>($"/api/mechanics/{m.Id}", Json))!.ServiceCount
        );
    }

    [Fact]
    public async Task InvalidContactIsRejectedWithoutChangingSavedData()
    {
        using var c = await SignInNewAccount();
        var m = await Create(c);
        foreach (
            var invalid in new[]
            {
                new MechanicInput(" "),
                new MechanicInput("Nome", Phone: "12"),
                new MechanicInput("Nome", Email: "email inválido"),
                new MechanicInput("Nome", Notes: new string('x', 4001)),
            }
        )
            Assert.Equal(
                HttpStatusCode.BadRequest,
                (await c.PutAsJsonAsync($"/api/mechanics/{m.Id}", invalid, Json)).StatusCode
            );
        Assert.Equal(
            "João Pereira",
            (await c.GetFromJsonAsync<MechanicDto>($"/api/mechanics/{m.Id}", Json))!.Name
        );
    }
}
