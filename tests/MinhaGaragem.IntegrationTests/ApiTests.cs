using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using MinhaGaragem.Application;
using MinhaGaragem.Domain;

namespace MinhaGaragem.IntegrationTests;

[Collection("api")]
public sealed class ApiTests(ApiFixture fixture)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    private HttpClient Client() =>
        fixture.Factory.CreateClient(
            new() { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false }
        );

    private static async Task Csrf(HttpClient c)
    {
        var token = await c.GetFromJsonAsync<JsonElement>("/api/auth/csrf");
        c.DefaultRequestHeaders.Remove("X-CSRF-TOKEN");
        c.DefaultRequestHeaders.Add("X-CSRF-TOKEN", token.GetProperty("token").GetString());
    }

    private async Task<HttpClient> SignInNewAccount()
    {
        var email = await fixture.CreateAccount("GarageTest1234");
        var c = Client();
        await Csrf(c);
        var response = await c.PostAsJsonAsync(
            "/api/auth/login",
            new { email, password = "GarageTest1234" }
        );
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        await Csrf(c);
        return c;
    }

    private static async Task<VehicleDto> Vehicle(HttpClient c) =>
        (await c.GetFromJsonAsync<Page<VehicleDto>>("/api/vehicles", Json))!.Items[0];

    private static async Task<ComponentDto[]> Components(HttpClient c, Guid v) =>
        (await c.GetFromJsonAsync<ComponentDto[]>($"/api/vehicles/{v}/components"))!;

    private static RecordInput Record(Guid[] ids) =>
        new(
            new DateOnly(2026, 1, 1),
            MaintenanceCategory.Engine,
            ServiceType.Replacement,
            "Troca de óleo e filtro",
            null,
            50,
            "Oficina de teste",
            null,
            [.. ids],
            [new("Óleo", 4, 25.50m), new("Filtro", 1, 20m)]
        );

    private static async Task<RecordDto> CreateRecord(HttpClient c, Guid v, RecordInput input)
    {
        var response = await c.PostAsJsonAsync($"/api/vehicles/{v}/records", input, Json);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<RecordDto>(Json))!;
    }

    [Fact]
    public async Task AnonymousCannotReadVehicles()
    {
        using var c = Client();
        Assert.Equal(HttpStatusCode.Unauthorized, (await c.GetAsync("/api/vehicles")).StatusCode);
    }

    [Fact]
    public async Task ProvisionedAccountCreatesOnlyKnownSaveiroFields()
    {
        using var c = await SignInNewAccount();
        var v = await Vehicle(c);
        Assert.Equal("Saveiro", v.Model);
        Assert.Equal(2013, v.ModelYear);
        Assert.Null(v.Mileage);
        Assert.Null(v.Plate);
        Assert.Null(v.Version);
        Assert.Equal("Prata", v.Color);
        Assert.Equal("#C0C0C0", v.ColorHex);
        Assert.Equal(PaintFinish.Metallic, v.PaintFinish);
        Assert.Equal(10, (await Components(c, v.Id)).Length);
    }

    [Fact]
    public async Task MutationsRequireCsrf()
    {
        using var c = await SignInNewAccount();
        var v = await Vehicle(c);
        c.DefaultRequestHeaders.Remove("X-CSRF-TOKEN");
        Assert.Equal(
            HttpStatusCode.BadRequest,
            (
                await c.PostAsJsonAsync($"/api/vehicles/{v.Id}/mileage", new { mileage = 1 })
            ).StatusCode
        );
    }

    [Fact]
    public async Task UsersCannotReadOrChangeOtherVehicles()
    {
        using var a = await SignInNewAccount();
        using var b = await SignInNewAccount();
        var va = await Vehicle(a);
        var vb = await Vehicle(b);
        Assert.Equal(
            HttpStatusCode.NotFound,
            (await b.GetAsync($"/api/vehicles/{va.Id}")).StatusCode
        );
        Assert.Equal(
            HttpStatusCode.NotFound,
            (
                await b.PostAsJsonAsync($"/api/vehicles/{va.Id}/mileage", new { mileage = 100 })
            ).StatusCode
        );
        Assert.DoesNotContain(
            (await b.GetFromJsonAsync<Page<VehicleDto>>("/api/vehicles", Json))!.Items,
            x => x.Id == va.Id
        );
        Assert.NotEqual(va.Id, vb.Id);
        foreach (
            var endpoint in new[]
            {
                "records",
                "components",
                "schedules",
                "mileage",
                "indicators",
                "timeline",
                "photo",
            }
        )
            Assert.Equal(
                HttpStatusCode.NotFound,
                (await b.GetAsync($"/api/vehicles/{va.Id}/{endpoint}")).StatusCode
            );
    }

    [Fact]
    public async Task MultiComponentServiceCountsOnceAndCanBeEdited()
    {
        using var c = await SignInNewAccount();
        var v = await Vehicle(c);
        var components = await Components(c, v.Id);
        var input = Record([components[0].Id, components[1].Id]);
        var r = await CreateRecord(c, v.Id, input);
        Assert.Equal(172m, r.TotalCost);
        var stats = (
            await c.GetFromJsonAsync<IndicatorsDto>(
                $"/api/vehicles/{v.Id}/indicators?year=2026",
                Json
            )
        )!;
        Assert.Equal(172m, stats.Total);
        Assert.Equal(1, stats.ServiceCount);
        var edited = await c.PutAsJsonAsync(
            $"/api/vehicles/{v.Id}/records/{r.Id}",
            input with
            {
                LaborCost = 80,
                Items = [new("Filtro novo", 1, 40)],
            },
            Json
        );
        Assert.Equal(HttpStatusCode.OK, edited.StatusCode);
        var after = (await edited.Content.ReadFromJsonAsync<RecordDto>(Json))!;
        Assert.Equal(120, after.TotalCost);
        Assert.Equal(2, after.Components.Count);
        Assert.Single(after.Items);
    }

    [Fact]
    public async Task PackageVisitKeepsPartsAndComponentsWithOneTotalAndCanChangePricingMode()
    {
        using var c = await SignInNewAccount();
        var v = await Vehicle(c);
        var components = await Components(c, v.Id);
        var engine = components.Single(x => x.Key == "engine");
        var suspension = components.Single(x => x.Key == "suspension");
        var input = Record([engine.Id, suspension.Id]) with
        {
            PackageTotal = 1000,
            LaborCost = 0,
            Items = [new("Amortecedores", 2, 0), new("Velas", 4, 0)],
            SubcomponentIds = [engine.Subcomponents![0].Id, suspension.Subcomponents![0].Id],
        };
        var saved = await CreateRecord(c, v.Id, input);
        Assert.Equal(1000, saved.TotalCost);
        Assert.Equal(1000, saved.PackageTotal);
        Assert.Null(saved.LaborCost);
        Assert.Null(saved.PartsCost);
        Assert.Equal(2, saved.Items.Count);
        Assert.All(saved.Items, item =>
        {
            Assert.Null(item.UnitCost);
            Assert.Null(item.Total);
        });
        var stats = (
            await c.GetFromJsonAsync<IndicatorsDto>(
                $"/api/vehicles/{v.Id}/indicators?year=2026",
                Json
            )
        )!;
        Assert.Equal(1000, stats.Total);
        Assert.Equal(1000, stats.Unallocated);
        Assert.Equal(0, stats.Labor);
        Assert.Equal(0, stats.Parts);
        Assert.Equal(1, stats.ServiceCount);
        foreach (var component in new[] { engine, suspension })
        {
            var summary = (
                await c.GetFromJsonAsync<ComponentSummaryDto>(
                    $"/api/vehicles/{v.Id}/components/{component.Id}/summary",
                    Json
                )
            )!;
            Assert.Equal(1000, summary.TotalCost);
            Assert.Equal(1, summary.ServiceCount);
        }
        var updated = await c.PutAsJsonAsync(
            $"/api/vehicles/{v.Id}/records/{saved.Id}",
            input with
            {
                PackageTotal = 1200,
            },
            Json
        );
        updated.EnsureSuccessStatusCode();
        Assert.Equal(1200, (await updated.Content.ReadFromJsonAsync<RecordDto>(Json))!.TotalCost);
        var detailed = await c.PutAsJsonAsync(
            $"/api/vehicles/{v.Id}/records/{saved.Id}",
            input with
            {
                PackageTotal = null,
                LaborCost = 100,
                Items = [new("Velas", 4, 25)],
            },
            Json
        );
        detailed.EnsureSuccessStatusCode();
        var result = (await detailed.Content.ReadFromJsonAsync<RecordDto>(Json))!;
        Assert.Equal(200, result.TotalCost);
        Assert.Equal(100, result.PartsCost);
        Assert.Null(result.PackageTotal);
        stats = (
            await c.GetFromJsonAsync<IndicatorsDto>(
                $"/api/vehicles/{v.Id}/indicators?year=2026",
                Json
            )
        )!;
        Assert.Equal(200, stats.Total);
        Assert.Equal(0, stats.Unallocated);
        Assert.Equal(1, stats.ServiceCount);
        var back = await c.PutAsJsonAsync(
            $"/api/vehicles/{v.Id}/records/{saved.Id}",
            input with
            {
                PackageTotal = 0,
            },
            Json
        );
        back.EnsureSuccessStatusCode();
        var free = (await back.Content.ReadFromJsonAsync<RecordDto>(Json))!;
        Assert.Equal(0, free.TotalCost);
        Assert.Equal(0, free.PackageTotal);
        Assert.Null(free.LaborCost);
    }

    [Fact]
    public async Task ConflictingPackageCostsAreRejectedWithoutChangingSavedRecord()
    {
        using var c = await SignInNewAccount();
        var v = await Vehicle(c);
        var component = (await Components(c, v.Id))[0];
        var input = Record([component.Id]);
        var original = await CreateRecord(c, v.Id, input);
        foreach (
            var invalid in new[]
            {
                input with
                {
                    PackageTotal = 1000,
                },
                input with
                {
                    PackageTotal = -1,
                    LaborCost = 0,
                    Items = [],
                },
                input with
                {
                    PackageTotal = 1.001m,
                    LaborCost = 0,
                    Items = [],
                },
            }
        )
            Assert.Equal(
                HttpStatusCode.BadRequest,
                (
                    await c.PutAsJsonAsync(
                        $"/api/vehicles/{v.Id}/records/{original.Id}",
                        invalid,
                        Json
                    )
                ).StatusCode
            );
        var rows = (
            await c.GetFromJsonAsync<Page<RecordDto>>($"/api/vehicles/{v.Id}/records", Json)
        )!;
        var current = Assert.Single(rows.Items);
        Assert.Equal(original.TotalCost, current.TotalCost);
        Assert.Null(current.PackageTotal);
    }

    [Fact]
    public async Task IndependentPartsAndLaborPaymentsPersistPendingDetailsAndUpdateOneVisit()
    {
        using var c = await SignInNewAccount();
        var v = await Vehicle(c);
        var components = await Components(c, v.Id);
        var mechanicResponse = await c.PostAsJsonAsync(
            "/api/mechanics",
            new MechanicInput("Profissional de teste"),
            Json
        );
        mechanicResponse.EnsureSuccessStatusCode();
        var mechanic = (await mechanicResponse.Content.ReadFromJsonAsync<MechanicDto>(Json))!;
        var input = Record([components[0].Id, components[1].Id]) with
        {
            LaborCost = 1000,
            MechanicId = mechanic.Id,
            Items =
            [
                new("Amortecedores de teste", 2, 300),
                new("Cabos de teste", 1, 200),
                new("Velas de teste", 4, 50),
                new("Kits de teste", 2, 250),
                new("Óleo de teste", 4, 125),
            ],
            Payments =
            [
                new(PaymentScope.Parts, PaymentMethod.CreditCard),
                new(PaymentScope.Labor, PaymentMethod.CreditCard),
            ],
        };
        var original = await CreateRecord(c, v.Id, input);
        Assert.Equal(2000, original.PartsCost);
        Assert.Equal(3000, original.BaseCost);
        Assert.Equal(3000, original.TotalCost);
        Assert.Equal(2, original.PendingPaymentCount);
        Assert.All(original.Payments!, p =>
        {
            Assert.Null(p.TotalCharged);
            Assert.Null(p.Interest);
            Assert.Null(p.Installments);
        });
        var ids = original.Payments!.Select(p => p.Id).Order().ToArray();
        var financed = input with
        {
            Payments =
            [
                new(PaymentScope.Parts, PaymentMethod.CreditCard, 6, 2200),
                new(PaymentScope.Labor, PaymentMethod.CreditCard, 3, 1100),
            ],
        };
        var response = await c.PutAsJsonAsync(
            $"/api/vehicles/{v.Id}/records/{original.Id}",
            financed,
            Json
        );
        response.EnsureSuccessStatusCode();
        var updated = (await response.Content.ReadFromJsonAsync<RecordDto>(Json))!;
        Assert.Equal(3300, updated.TotalCost);
        Assert.Equal(300, updated.InterestCost);
        Assert.Equal(0, updated.PendingPaymentCount);
        Assert.Equal(ids, updated.Payments!.Select(p => p.Id).Order());
        Assert.Equal(5, updated.Items.Count);
        Assert.Equal(2000m, updated.Items.Sum(i => i.Total));
        var stats = (
            await c.GetFromJsonAsync<IndicatorsDto>(
                $"/api/vehicles/{v.Id}/indicators?year=2026",
                Json
            )
        )!;
        Assert.Equal(3300, stats.Total);
        Assert.Equal(2000, stats.Parts);
        Assert.Equal(1000, stats.Labor);
        Assert.Equal(300, stats.Interest);
        Assert.Equal(1, stats.ServiceCount);
        var history = (
            await c.GetFromJsonAsync<Page<RecordDto>>($"/api/mechanics/{mechanic.Id}/records", Json)
        )!;
        Assert.Equal(3300, Assert.Single(history.Items).TotalCost);
        // Older clients may omit the new field; omission must preserve the stored charges.
        response = await c.PutAsJsonAsync(
            $"/api/vehicles/{v.Id}/records/{original.Id}",
            input with
            {
                Description = "Observações revisadas",
                Payments = null,
            },
            Json
        );
        response.EnsureSuccessStatusCode();
        updated = (await response.Content.ReadFromJsonAsync<RecordDto>(Json))!;
        Assert.Equal(3300, updated.TotalCost);
        Assert.Equal(2, updated.Payments!.Count);
        response = await c.PutAsJsonAsync(
            $"/api/vehicles/{v.Id}/records/{original.Id}",
            input with
            {
                Payments = [],
            },
            Json
        );
        response.EnsureSuccessStatusCode();
        updated = (await response.Content.ReadFromJsonAsync<RecordDto>(Json))!;
        Assert.Empty(updated.Payments!);
        Assert.Equal(3000, updated.TotalCost);
    }

    [Fact]
    public async Task ExistingVisitCanAddAndRemovePaymentsAfterCreation()
    {
        using var c = await SignInNewAccount();
        var v = await Vehicle(c);
        var input = Record([(await Components(c, v.Id))[0].Id]);
        var original = await CreateRecord(c, v.Id, input);
        Assert.Empty(original.Payments!);
        var financed = input with
        {
            Payments =
            [
                new(PaymentScope.Parts, PaymentMethod.CreditCard, 2, 140),
                new(PaymentScope.Labor, PaymentMethod.CreditCard, 2, 60),
            ],
        };
        for (var attempt = 0; attempt < 2; attempt++)
        {
            var response = await c.PutAsJsonAsync(
                $"/api/vehicles/{v.Id}/records/{original.Id}",
                financed,
                Json
            );
            response.EnsureSuccessStatusCode();
            var changed = (await response.Content.ReadFromJsonAsync<RecordDto>(Json))!;
            Assert.Equal(2, changed.Payments!.Count);
            Assert.Equal(200, changed.TotalCost);
            response = await c.PutAsJsonAsync(
                $"/api/vehicles/{v.Id}/records/{original.Id}",
                input with
                {
                    Payments = [],
                },
                Json
            );
            response.EnsureSuccessStatusCode();
            changed = (await response.Content.ReadFromJsonAsync<RecordDto>(Json))!;
            Assert.Empty(changed.Payments!);
            Assert.Equal(original.TotalCost, changed.TotalCost);
        }
    }

    [Fact]
    public async Task InvalidPaymentUpdatesKeepSavedDataAndRespectOwnership()
    {
        using var c = await SignInNewAccount();
        var v = await Vehicle(c);
        var input = Record([(await Components(c, v.Id))[0].Id]) with
        {
            Payments = [new(PaymentScope.Labor, PaymentMethod.CreditCard, 2, 60)],
        };
        var original = await CreateRecord(c, v.Id, input);
        foreach (
            var invalid in new[]
            {
                input with
                {
                    Payments = [new(PaymentScope.Labor, PaymentMethod.CreditCard, 2, 49)],
                },
                input with
                {
                    Payments =
                    [
                        new(PaymentScope.Labor, PaymentMethod.CreditCard),
                        new(PaymentScope.Labor, PaymentMethod.CreditCard),
                    ],
                },
                input with
                {
                    Payments = [new(PaymentScope.Package, PaymentMethod.CreditCard)],
                },
                input with
                {
                    Payments = [new(PaymentScope.Labor, PaymentMethod.CreditCard, 0, 60)],
                },
                input with
                {
                    LaborCost = 100,
                    Payments = null,
                },
            }
        )
            Assert.Equal(
                HttpStatusCode.BadRequest,
                (
                    await c.PutAsJsonAsync(
                        $"/api/vehicles/{v.Id}/records/{original.Id}",
                        invalid,
                        Json
                    )
                ).StatusCode
            );
        using var other = await SignInNewAccount();
        Assert.Equal(
            HttpStatusCode.NotFound,
            (await other.GetAsync($"/api/vehicles/{v.Id}/records/{original.Id}")).StatusCode
        );
        Assert.Equal(
            HttpStatusCode.NotFound,
            (
                await other.PutAsJsonAsync(
                    $"/api/vehicles/{v.Id}/records/{original.Id}",
                    input,
                    Json
                )
            ).StatusCode
        );
        var saved = (
            await c.GetFromJsonAsync<RecordDto>($"/api/vehicles/{v.Id}/records/{original.Id}", Json)
        )!;
        Assert.Equal(original.TotalCost, saved.TotalCost);
        Assert.Equal(60, Assert.Single(saved.Payments!).TotalCharged);
    }

    [Fact]
    public async Task CrossVehicleComponentsAreRejected()
    {
        using var a = await SignInNewAccount();
        using var b = await SignInNewAccount();
        var va = await Vehicle(a);
        var vb = await Vehicle(b);
        var foreign = (await Components(b, vb.Id))[0];
        Assert.Equal(
            HttpStatusCode.BadRequest,
            (
                await a.PostAsJsonAsync(
                    $"/api/vehicles/{va.Id}/records",
                    Record([foreign.Id]),
                    Json
                )
            ).StatusCode
        );
    }

    [Fact]
    public async Task MileageIsMonotonicAndRecorded()
    {
        using var c = await SignInNewAccount();
        var v = await Vehicle(c);
        Assert.Equal(
            HttpStatusCode.NoContent,
            (
                await c.PostAsJsonAsync($"/api/vehicles/{v.Id}/mileage", new { mileage = 5000 })
            ).StatusCode
        );
        Assert.Equal(
            HttpStatusCode.BadRequest,
            (
                await c.PostAsJsonAsync($"/api/vehicles/{v.Id}/mileage", new { mileage = 4999 })
            ).StatusCode
        );
        var history = await c.GetFromJsonAsync<JsonElement>($"/api/vehicles/{v.Id}/mileage");
        Assert.Equal(1, history.GetProperty("total").GetInt32());
        Assert.Equal(5000, (await Vehicle(c)).Mileage);
    }

    [Fact]
    public async Task SchedulesRecalculateAndIsolateUpdates()
    {
        using var c = await SignInNewAccount();
        var v = await Vehicle(c);
        var comp = (await Components(c, v.Id))[0];
        var input = new ScheduleInput(comp.Id, "Revisar", null, 6000, SchedulePriority.High, false);
        var response = await c.PostAsJsonAsync($"/api/vehicles/{v.Id}/schedules", input, Json);
        response.EnsureSuccessStatusCode();
        var s = (await response.Content.ReadFromJsonAsync<ScheduleDto>(Json))!;
        Assert.Equal(ScheduleState.Current, s.State);
        await c.PostAsJsonAsync($"/api/vehicles/{v.Id}/mileage", new { mileage = 6000 });
        var rows = (
            await c.GetFromJsonAsync<Page<ScheduleDto>>($"/api/vehicles/{v.Id}/schedules", Json)
        )!;
        Assert.Equal(ScheduleState.Overdue, rows.Items[0].State);
        using var other = await SignInNewAccount();
        Assert.Equal(
            HttpStatusCode.NotFound,
            (
                await other.PutAsJsonAsync($"/api/vehicles/{v.Id}/schedules/{s.Id}", input, Json)
            ).StatusCode
        );
    }

    [Fact]
    public async Task AttachmentsValidateTypeAndArePrivate()
    {
        using var a = await SignInNewAccount();
        using var b = await SignInNewAccount();
        var v = await Vehicle(a);
        var comp = (await Components(a, v.Id))[0];
        var r = await CreateRecord(a, v.Id, Record([comp.Id]));
        using var invalid = new MultipartFormDataContent();
        invalid.Add(new ByteArrayContent("<script>bad</script>"u8.ToArray()), "file", "nota.pdf");
        Assert.Equal(
            HttpStatusCode.BadRequest,
            (
                await a.PostAsync($"/api/vehicles/{v.Id}/records/{r.Id}/attachments", invalid)
            ).StatusCode
        );
        using var form = new MultipartFormDataContent();
        form.Add(new ByteArrayContent("%PDF-1.7 Test fixture"u8.ToArray()), "file", "nota.pdf");
        var response = await a.PostAsync($"/api/vehicles/{v.Id}/records/{r.Id}/attachments", form);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var att = (await response.Content.ReadFromJsonAsync<AttachmentDto>())!;
        Assert.Equal(HttpStatusCode.OK, (await a.GetAsync(att.Url)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await b.GetAsync(att.Url)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await b.DeleteAsync(att.Url)).StatusCode);
    }

    [Fact]
    public async Task HistorySupportsFilterAndPagination()
    {
        using var c = await SignInNewAccount();
        var v = await Vehicle(c);
        var comp = (await Components(c, v.Id))[0];
        await CreateRecord(c, v.Id, Record([comp.Id]));
        var yes = await c.GetFromJsonAsync<Page<RecordDto>>(
            $"/api/vehicles/{v.Id}/records?component={comp.Id}&pageSize=1",
            Json
        );
        Assert.Equal(1, yes!.Total);
        Assert.Single(yes.Items);
        var no = await c.GetFromJsonAsync<Page<RecordDto>>(
            $"/api/vehicles/{v.Id}/records?from=2026-02-01",
            Json
        );
        Assert.Empty(no!.Items);
        Assert.Equal(
            HttpStatusCode.BadRequest,
            (await c.GetAsync($"/api/vehicles/{v.Id}/records?pageSize=1000")).StatusCode
        );
    }

    [Fact]
    public async Task DeletingVehicleCascadesServicesComponentsAndSchedules()
    {
        using var c = await SignInNewAccount();
        var v = await Vehicle(c);
        var comp = (await Components(c, v.Id))[0];
        await CreateRecord(c, v.Id, Record([comp.Id]));
        await c.PostAsJsonAsync(
            $"/api/vehicles/{v.Id}/schedules",
            new ScheduleInput(
                comp.Id,
                "Próxima revisão",
                new(2027, 1, 1),
                null,
                SchedulePriority.Normal,
                false
            ),
            Json
        );
        Assert.Equal(
            HttpStatusCode.NoContent,
            (await c.DeleteAsync($"/api/vehicles/{v.Id}")).StatusCode
        );
        Assert.Empty((await c.GetFromJsonAsync<Page<VehicleDto>>("/api/vehicles", Json))!.Items);
    }

    [Fact]
    public async Task DetailedServicesKeepOneCostAndFilterByEachSubcomponent()
    {
        using var c = await SignInNewAccount();
        var v = await Vehicle(c);
        var components = await Components(c, v.Id);
        var engine = components.Single(x => x.Key == "engine");
        var suspension = components.Single(x => x.Key == "suspension");
        Assert.Equal(6, suspension.Subcomponents!.Count);
        var plugs = engine.Subcomponents!.Single(s => s.Key == "spark-plugs");
        var cables = engine.Subcomponents!.Single(s => s.Key == "ignition-cables");
        var engineInput = Record([engine.Id]) with
        {
            Description = "Troca de cabos e velas",
            SubcomponentIds = [plugs.Id, cables.Id],
            PrimaryComponentId = engine.Id,
            Notes = "Peças conferidas na oficina",
        };
        var r = await CreateRecord(c, v.Id, engineInput);
        Assert.Equal(engine.Id, r.PrimaryComponentId);
        Assert.Equal(2, r.Subcomponents.Count);
        Assert.Equal(172, r.TotalCost);
        foreach (var sub in new[] { plugs, cables })
        {
            var history = (
                await c.GetFromJsonAsync<Page<RecordDto>>(
                    $"/api/vehicles/{v.Id}/records?subcomponent={sub.Id}",
                    Json
                )
            )!;
            Assert.Equal(1, history.Total);
            Assert.Equal(r.Id, Assert.Single(history.Items).Id);
        }
        var shock = suspension.Subcomponents.Single(s => s.Key == "shock-absorbers");
        await CreateRecord(
            c,
            v.Id,
            Record([suspension.Id]) with
            {
                Description = "Troca de amortecedores",
                Category = MaintenanceCategory.Suspension,
                SubcomponentIds = [shock.Id],
                Items = [new("Amortecedor", 2, 150)],
                LaborCost = 100,
            }
        );
        var indicators = (
            await c.GetFromJsonAsync<IndicatorsDto>($"/api/vehicles/{v.Id}/indicators", Json)
        )!;
        Assert.Equal(572, indicators.Total);
        Assert.Equal(2, indicators.ServiceCount);
        var plan = new ScheduleInput(
            engine.Id,
            "Revisar ignição",
            new(2027, 1, 1),
            null,
            SchedulePriority.Normal,
            false,
            plugs.Id
        );
        (
            await c.PostAsJsonAsync($"/api/vehicles/{v.Id}/schedules", plan, Json)
        ).EnsureSuccessStatusCode();
        var summary = (
            await c.GetFromJsonAsync<ComponentSummaryDto>(
                $"/api/vehicles/{v.Id}/components/{engine.Id}/summary?subcomponent={plugs.Id}",
                Json
            )
        )!;
        Assert.Equal(172, summary.TotalCost);
        Assert.Equal(1, summary.ServiceCount);
        Assert.Equal("Peças conferidas na oficina", summary.LastService!.Notes);
        Assert.Equal(plugs.Id, summary.NextMaintenance!.SubcomponentId);
        var edited = await c.PutAsJsonAsync(
            $"/api/vehicles/{v.Id}/records/{r.Id}",
            engineInput with
            {
                SubcomponentIds = [plugs.Id],
                LaborCost = 80,
            },
            Json
        );
        edited.EnsureSuccessStatusCode();
        Assert.Single((await edited.Content.ReadFromJsonAsync<RecordDto>(Json))!.Subcomponents);
        Assert.Equal(
            0,
            (
                await c.GetFromJsonAsync<Page<RecordDto>>(
                    $"/api/vehicles/{v.Id}/records?subcomponent={cables.Id}",
                    Json
                )
            )!.Total
        );
        var moved = await c.PutAsJsonAsync(
            $"/api/vehicles/{v.Id}/records/{r.Id}",
            engineInput with
            {
                ComponentIds = [suspension.Id],
                PrimaryComponentId = suspension.Id,
                SubcomponentIds = [shock.Id],
            },
            Json
        );
        moved.EnsureSuccessStatusCode();
        Assert.Equal(
            shock.Id,
            Assert
                .Single((await moved.Content.ReadFromJsonAsync<RecordDto>(Json))!.Subcomponents)
                .Id
        );
        (await c.DeleteAsync($"/api/vehicles/{v.Id}")).EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task DetailedLinksRejectWrongSystemDuplicatesAndOtherVehicles()
    {
        using var a = await SignInNewAccount();
        using var b = await SignInNewAccount();
        var va = await Vehicle(a);
        var vb = await Vehicle(b);
        var components = await Components(a, va.Id);
        var engine = components.Single(c => c.Key == "engine");
        var suspension = components.Single(c => c.Key == "suspension");
        var foreign = (await Components(b, vb.Id)).Single(c => c.Key == "engine").Subcomponents![0];
        var sub = engine.Subcomponents![0];
        foreach (
            var input in new[]
            {
                Record([engine.Id]) with
                {
                    SubcomponentIds = [foreign.Id],
                },
                Record([suspension.Id]) with
                {
                    SubcomponentIds = [sub.Id],
                },
                Record([engine.Id]) with
                {
                    SubcomponentIds = [sub.Id, sub.Id],
                },
                Record([engine.Id]) with
                {
                    PrimaryComponentId = suspension.Id,
                },
            }
        )
            Assert.Equal(
                HttpStatusCode.BadRequest,
                (await a.PostAsJsonAsync($"/api/vehicles/{va.Id}/records", input, Json)).StatusCode
            );
        Assert.Equal(
            HttpStatusCode.BadRequest,
            (
                await a.PostAsJsonAsync(
                    $"/api/vehicles/{va.Id}/schedules",
                    new ScheduleInput(
                        suspension.Id,
                        "Revisar",
                        new(2027, 1, 1),
                        null,
                        SchedulePriority.Normal,
                        false,
                        sub.Id
                    ),
                    Json
                )
            ).StatusCode
        );
        Assert.Equal(
            HttpStatusCode.NotFound,
            (await b.GetAsync($"/api/vehicles/{va.Id}/components/{engine.Id}/summary")).StatusCode
        );
        Assert.Equal(
            HttpStatusCode.NotFound,
            (
                await a.GetAsync(
                    $"/api/vehicles/{va.Id}/components/{engine.Id}/summary?subcomponent={foreign.Id}"
                )
            ).StatusCode
        );
    }

    [Fact]
    public async Task ComponentSummaryIncludesCostsBeyondRecentFiveRecords()
    {
        using var c = await SignInNewAccount();
        var v = await Vehicle(c);
        var component = (await Components(c, v.Id))[0];
        for (var i = 0; i < 7; i++)
            await CreateRecord(c, v.Id, Record([component.Id]));
        var summary = (
            await c.GetFromJsonAsync<ComponentSummaryDto>(
                $"/api/vehicles/{v.Id}/components/{component.Id}/summary",
                Json
            )
        )!;
        Assert.Equal(7, summary.ServiceCount);
        Assert.Equal(1204, summary.TotalCost);
        Assert.Equal(5, summary.Recent.Count);
        Assert.Null(summary.NextMaintenance);
    }

    [Fact]
    public async Task PaintPersistsAndKeepsMileageAndOtherVehicleFields()
    {
        using var c = await SignInNewAccount();
        using var other = await SignInNewAccount();
        var v = await Vehicle(c);
        await c.PostAsJsonAsync($"/api/vehicles/{v.Id}/mileage", new { mileage = 120000 });
        var input = new PaintInput("Azul", "#214b80", PaintFinish.Metallic);
        Assert.Equal(
            HttpStatusCode.NotFound,
            (await other.PutAsJsonAsync($"/api/vehicles/{v.Id}/paint", input, Json)).StatusCode
        );
        (
            await c.PutAsJsonAsync($"/api/vehicles/{v.Id}/paint", input, Json)
        ).EnsureSuccessStatusCode();
        var saved = await Vehicle(c);
        Assert.Equal("#214B80", saved.ColorHex);
        Assert.Equal("Azul", saved.Color);
        Assert.Equal(120000, saved.Mileage);
        Assert.Equal(v.ModelYear, saved.ModelYear);
        foreach (
            var invalid in new[]
            {
                input with
                {
                    ColorHex = "red",
                },
                input with
                {
                    Color = " ",
                },
                input with
                {
                    PaintFinish = (PaintFinish)999,
                },
            }
        )
            Assert.Equal(
                HttpStatusCode.BadRequest,
                (await c.PutAsJsonAsync($"/api/vehicles/{v.Id}/paint", invalid, Json)).StatusCode
            );
        Assert.Equal("#214B80", (await Vehicle(c)).ColorHex);
    }

    [Fact]
    public async Task LoginLogoutAndHealthChecksWork()
    {
        using var c = Client();
        await Csrf(c);
        var email = await fixture.CreateAccount("GarageTest1234");
        var data = new { email, password = "GarageTest1234" };
        (await c.PostAsJsonAsync("/api/auth/login", data)).EnsureSuccessStatusCode();
        await Csrf(c);
        Assert.Equal(
            HttpStatusCode.NoContent,
            (await c.PostAsJsonAsync("/api/auth/logout", new { })).StatusCode
        );
        Assert.Equal(HttpStatusCode.Unauthorized, (await c.GetAsync("/api/auth/me")).StatusCode);
        await Csrf(c);
        Assert.Equal(
            HttpStatusCode.OK,
            (await c.PostAsJsonAsync("/api/auth/login", data)).StatusCode
        );
        Assert.Equal(HttpStatusCode.OK, (await c.GetAsync("/health/ready")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await c.GetAsync("/health/live")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await c.GetAsync("/openapi/v1.json")).StatusCode);
    }
}
