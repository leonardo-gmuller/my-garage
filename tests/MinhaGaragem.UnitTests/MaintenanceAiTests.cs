using System.Text.Json;
using System.Text.Json.Nodes;
using MinhaGaragem.Application.AI;
using MinhaGaragem.Domain;

namespace MinhaGaragem.UnitTests;

[Trait("Category", "AI")]
public sealed class MaintenanceAiTests
{
    private const string Message =
        "Troquei 2 amortecedores dianteiros, cabos e velas em 20/11/2026 na Oficina Centro. Paguei R$ 850,50 e o carro estava com 125 mil quilômetros. Conferir alinhamento.";
    private static readonly DateOnly Today = new(2026, 11, 23);

    private static MaintenanceExtraction Extraction() =>
        new()
        {
            Description = "Troca de amortecedores, cabos e velas",
            Category = MaintenanceCategory.General,
            ServiceType = ServiceType.Replacement,
            Mileage = 125000,
            MileageEvidence = "125 mil quilômetros",
            TotalCost = 850.50m,
            TotalCostEvidence = "R$ 850,50",
            ServiceDate = new(2026, 11, 20),
            ServiceDateEvidence = "20/11/2026",
            Workshop = "Oficina Centro",
            Notes = "Conferir alinhamento.",
            Components =
            [
                new()
                {
                    Key = "suspension",
                    SubcomponentKeys = ["shock-absorbers"],
                    Evidence = "amortecedores dianteiros",
                },
                new()
                {
                    Key = "engine",
                    SubcomponentKeys = ["ignition-cables", "spark-plugs"],
                    Evidence = "cabos e velas",
                },
            ],
            Items =
            [
                new()
                {
                    Name = "amortecedores dianteiros",
                    Quantity = 2,
                    Evidence = "2 amortecedores dianteiros",
                    QuantityEvidence = "2",
                },
                new()
                {
                    Name = "cabos",
                    Quantity = null,
                    Evidence = "cabos",
                    QuantityEvidence = null,
                },
                new()
                {
                    Name = "velas",
                    Quantity = null,
                    Evidence = "velas",
                    QuantityEvidence = null,
                },
            ],
            UnmatchedComponents = [],
            Warnings = [],
        };

    private static MaintenanceAiDraft Parse(string json)
    {
        var vehicle = new Vehicle { Mileage = 130000 };
        var catalogue = ComponentCatalog
            .All.Select(c => SubcomponentCatalog.CreateComponent(vehicle.Id, c.Key, c.Name))
            .ToArray();
        var result = MaintenanceDraftValidator.Parse(json, Message, vehicle, catalogue, Today);
        Assert.Equal(vehicle.Id, result.VehicleId);
        Assert.All(result.Components, c => Assert.Contains(catalogue, known => known.Id == c.Id));
        return result;
    }

    private static string Json(MaintenanceExtraction data) =>
        JsonSerializer.Serialize(data, MaintenanceAiJson.Options);

    [Fact]
    public void SourceEvidenceAndRealCatalogueResolveComponentsWithoutSplittingTheTotal()
    {
        var draft = Parse(Json(Extraction()));
        Assert.Equal(850.50m, draft.TotalCost);
        Assert.Equal(125000, draft.Mileage);
        Assert.Equal(2, draft.Components.Count);
        Assert.Equal(3, draft.Components.Sum(c => c.Subcomponents.Count));
        Assert.Equal(2, draft.Items[0].Quantity);
        Assert.Null(draft.Items[1].Quantity);
        Assert.Contains("itemQuantities", draft.MissingFields);
        Assert.Equal(new DateOnly(2026, 11, 20), draft.ServiceDate);
    }

    [Fact]
    public void MissingValuesRemainNullAndAreReportedByTheServer()
    {
        var draft = Parse(
            Json(
                Extraction() with
                {
                    Mileage = null,
                    MileageEvidence = null,
                    TotalCost = null,
                    TotalCostEvidence = null,
                    ServiceDate = null,
                    ServiceDateEvidence = null,
                    Workshop = null,
                    Notes = null,
                    Category = null,
                    ServiceType = null,
                }
            )
        );
        Assert.Null(draft.TotalCost);
        Assert.Null(draft.Mileage);
        Assert.Null(draft.ServiceDate);
        Assert.Null(draft.Category);
        Assert.Null(draft.ServiceType);
        Assert.Contains("serviceDate", draft.MissingFields);
        Assert.Contains("totalCost", draft.MissingFields);
        Assert.Contains("workshop", draft.MissingFields);
    }

    [Theory]
    [InlineData("mileage", "-1")]
    [InlineData("mileage", "10000001")]
    [InlineData("mileage", "140000")]
    [InlineData("mileage", "124000")]
    [InlineData("totalCost", "-1")]
    [InlineData("totalCost", "850.501")]
    [InlineData("totalCost", "900")]
    [InlineData("category", "\"invented\"")]
    [InlineData("category", "1")]
    [InlineData("serviceType", "\"invented\"")]
    [InlineData("serviceDate", "\"2029-01-01\"")]
    [InlineData("serviceDate", "\"2026-11-19\"")]
    [InlineData("workshop", "\"Oficina inventada\"")]
    [InlineData("notes", "\"Trocar motor\"")]
    [InlineData("components", "null")]
    [InlineData("items", "null")]
    [InlineData("warnings", "null")]
    [InlineData("vehicleId", "\"invented-id\"")]
    public void IncompatibleOrUnsupportedModelDataIsRejected(string property, string value)
    {
        var json = JsonNode.Parse(Json(Extraction()))!;
        json[property] = JsonNode.Parse(value);
        Assert.Equal(
            AiFailure.InvalidResponse,
            Assert.Throws<MaintenanceAiException>(() => Parse(json.ToJsonString())).Failure
        );
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("null")]
    [InlineData("not json")]
    [InlineData("```json\n{}\n```")]
    public void MalformedOrIncompleteResponsesAreRejected(string json) =>
        Assert.Throws<MaintenanceAiException>(() => Parse(json));

    [Fact]
    public void UnknownComponentsAndWrongSubcomponentParentsAreRejected()
    {
        var data = Extraction();
        Assert.Throws<MaintenanceAiException>(() =>
            Parse(
                Json(
                    data with
                    {
                        Components =
                        [
                            new()
                            {
                                Key = "unknown",
                                SubcomponentKeys = [],
                                Evidence = "cabos",
                            },
                        ],
                    }
                )
            )
        );
        Assert.Throws<MaintenanceAiException>(() =>
            Parse(
                Json(
                    data with
                    {
                        Components =
                        [
                            new()
                            {
                                Key = "suspension",
                                SubcomponentKeys = ["spark-plugs"],
                                Evidence = "velas",
                            },
                        ],
                    }
                )
            )
        );
        Assert.Throws<MaintenanceAiException>(() =>
            Parse(Json(data with { Components = [data.Components[0], data.Components[0]] }))
        );
    }

    [Fact]
    public void FabricatedPartBrandsQuantitiesAndMissingEvidenceAreRejected()
    {
        var item = Extraction().Items[0];
        foreach (
            var invalid in new[]
            {
                item with
                {
                    Name = "Amortecedor Marca Inventada",
                },
                item with
                {
                    Quantity = 3,
                },
                item with
                {
                    Quantity = 0,
                },
                item with
                {
                    Evidence = "foi mencionado",
                },
                item with
                {
                    QuantityEvidence = null,
                },
            }
        )
            Assert.Throws<MaintenanceAiException>(() =>
                Parse(Json(Extraction() with { Items = [invalid] }))
            );
    }

    [Fact]
    public void AmbiguousAssociationsRemainVisibleForHumanReview()
    {
        var draft = Parse(
            Json(
                Extraction() with
                {
                    Components = [],
                    UnmatchedComponents = ["cabos"],
                    Warnings = ["Confirme o conjunto envolvido."],
                }
            )
        );
        Assert.Empty(draft.Components);
        Assert.Contains("components", draft.MissingFields);
        Assert.Equal(2, draft.Warnings.Count);
    }

    [Fact]
    public void DailyBudgetsAndConcurrencyAreBoundedAndResetAtUtcMidnight()
    {
        var clock = new TestClock();
        var gate = new MaintenanceAiGate(new(RequestsPerDay: 3, RequestsPerUserPerDay: 1), clock);
        var first = Guid.NewGuid();
        var second = Guid.NewGuid();
        using (var a = gate.Enter(first))
        using (var b = gate.Enter(second))
            Assert.Throws<MaintenanceAiException>(() => gate.Enter(Guid.NewGuid()));
        Assert.Throws<MaintenanceAiException>(() => gate.Enter(first));
        using (var last = gate.Enter(Guid.NewGuid())) { }
        Assert.Throws<MaintenanceAiException>(() => gate.Enter(Guid.NewGuid()));
        clock.Now = clock.Now.AddDays(1);
        using var reset = gate.Enter(first);
    }

    private sealed class TestClock : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = new(2026, 11, 23, 12, 0, 0, TimeSpan.Zero);

        public override DateTimeOffset GetUtcNow() => Now;
    }
}
