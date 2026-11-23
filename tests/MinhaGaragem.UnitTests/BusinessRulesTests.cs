using MinhaGaragem.Application;
using MinhaGaragem.Domain;

namespace MinhaGaragem.UnitTests;

public class BusinessRulesTests
{
    [Fact]
    public void CostsAreNotDuplicatedAcrossComponents()
    {
        var r = new MaintenanceRecord
        {
            Date = new(2026, 11, 1),
            LaborCost = 80m,
            Items =
            [
                new()
                {
                    Name = "Óleo",
                    Quantity = 4,
                    UnitCost = 35.50m,
                },
            ],
            Components =
            [
                new()
                {
                    Component = new() { Key = "engine", Name = "Motor" },
                },
                new()
                {
                    Component = new() { Key = "cooling", Name = "Arrefecimento" },
                },
            ],
        };
        var result = FinancialSummary.Calculate([r], 2026);
        Assert.Equal(142m, result.Parts);
        Assert.Equal(80m, result.Labor);
        Assert.Equal(222m, result.Total);
        Assert.Equal(222m, result.Monthly.Single(x => x.Month == "2026-11").Total);
        Assert.Equal(1, result.ServiceCount);
    }

    [Fact]
    public void FinancialSummarySeparatesYearsAndIncludesEmptyMonths()
    {
        var r = FinancialSummary.Calculate(
            [
                new() { Date = new(2025, 12, 1), LaborCost = 100 },
                new() { Date = new(2026, 1, 1), LaborCost = 20 },
            ],
            2026
        );
        Assert.Equal(120, r.Total);
        Assert.Equal(12, r.Monthly.Count);
        Assert.Equal(20, r.Monthly.Sum(x => x.Total));
        Assert.Equal(2, r.Annual.Count);
    }

    [Fact]
    public void EmptySummaryHasNoInventedMaintenance()
    {
        var r = FinancialSummary.Calculate([], 2026);
        Assert.Equal(0, r.Total);
        Assert.Null(r.LastMaintenance);
    }

    [Fact]
    public void PackageTotalsAreCountedOnceAndDoNotInventItemPrices()
    {
        var package = new MaintenanceRecord
        {
            Date = new(2026, 11, 1),
            PackageTotal = 1000,
            Items =
            [
                new() { Name = "Amortecedores", Quantity = 2 },
                new() { Name = "Velas", Quantity = 4 },
            ],
        };
        var summary = FinancialSummary.Calculate(
            [
                package,
                new()
                {
                    Date = new(2026, 11, 2),
                    LaborCost = 80,
                    Items = [new() { Name = "Filtro", UnitCost = 20 }],
                },
            ],
            2026
        );
        Assert.Equal(1100, summary.Total);
        Assert.Equal(20, summary.Parts);
        Assert.Equal(80, summary.Labor);
        Assert.Equal(1000, summary.Unallocated);
        var month = summary.Monthly.Single(m => m.Month == "2026-11");
        Assert.Equal(month.Total, month.Parts + month.Labor + month.Unallocated);
        var dto = package.ToDto();
        Assert.Null(dto.PartsCost);
        Assert.Null(dto.LaborCost);
        Assert.All(dto.Items, item =>
        {
            Assert.Null(item.Total);
            Assert.Null(item.UnitCost);
        });
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(1.001)]
    [InlineData(100000001)]
    public void PackageTotalMustBeValidMoney(decimal value) =>
        Assert.Throws<DomainException>(() =>
            Rules.Record(
                ValidRecord() with
                {
                    PackageTotal = value,
                    LaborCost = 0,
                    Items = [],
                },
                1000,
                new(2026, 11, 18)
            )
        );

    [Fact]
    public void PackageTotalRejectsConflictingDetailedPrices()
    {
        Assert.Throws<DomainException>(() =>
            Rules.Record(ValidRecord() with { PackageTotal = 1000 }, 1000, new(2026, 11, 18))
        );
        Assert.Throws<DomainException>(() =>
            Rules.Record(
                ValidRecord() with
                {
                    PackageTotal = 1000,
                    LaborCost = 0,
                },
                1000,
                new(2026, 11, 18)
            )
        );
        Rules.Record(
            ValidRecord() with
            {
                PackageTotal = 0,
                LaborCost = 0,
                Items = [new("Peça sem preço separado", 1, 0)],
            },
            1000,
            new(2026, 11, 18)
        );
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(99)]
    [InlineData(100)]
    public void MileageCannotDecreaseOrRepeat(int mileage)
    {
        var v = new Vehicle { Mileage = 100 };
        Assert.Throws<DomainException>(() => v.UpdateMileage(mileage));
    }

    [Fact]
    public void UnknownMileageCanBecomeZero()
    {
        var v = new Vehicle();
        v.UpdateMileage(0);
        Assert.Equal(0, v.Mileage);
    }

    [Fact]
    public void MileageCanIncrease()
    {
        var v = new Vehicle { Mileage = 100 };
        v.UpdateMileage(101);
        Assert.Equal(101, v.Mileage);
    }

    [Theory]
    [InlineData(-1, 11000, "Overdue")]
    [InlineData(0, 11001, "DueSoon")]
    [InlineData(30, 11001, "DueSoon")]
    [InlineData(31, 11001, "Current")]
    [InlineData(31, 11000, "DueSoon")]
    [InlineData(100, 10000, "Overdue")]
    public void ScheduleUsesFirstReachedLimit(int days, int dueMileage, string expected)
    {
        var today = new DateOnly(2026, 11, 18);
        var s = new MaintenanceSchedule { DueDate = today.AddDays(days), DueMileage = dueMileage };
        Assert.Equal(expected, s.State(today, 10000).ToString());
    }

    [Fact]
    public void UnknownOdometerDoesNotPretendMileageAlert()
    {
        var s = new MaintenanceSchedule { DueMileage = 100 };
        Assert.Equal(ScheduleState.Current, s.State(new(2026, 11, 18), null));
    }

    [Fact]
    public void CompletedScheduleStaysCompleted()
    {
        var s = new MaintenanceSchedule { Completed = true, DueDate = new(2020, 1, 1) };
        Assert.Equal(ScheduleState.Completed, s.State(new(2026, 11, 18), 90000));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(1.001)]
    [InlineData(100000001)]
    public void MoneyRejectsInvalidValues(decimal value) =>
        Assert.Throws<DomainException>(() => Rules.Money(value));

    private static RecordInput ValidRecord() =>
        new(
            new(2026, 11, 1),
            MaintenanceCategory.Engine,
            ServiceType.Preventive,
            "Troca de óleo",
            1000,
            50,
            null,
            null,
            [Guid.NewGuid()],
            [new("Filtro", 1, 30)]
        );

    [Fact]
    public void RejectFutureRecord()
    {
        var r = ValidRecord() with { Date = new(2027, 1, 1) };
        Assert.Throws<DomainException>(() => Rules.Record(r, 1000, new(2026, 11, 18)));
    }

    [Fact]
    public void RejectRecordBeyondOdometer()
    {
        Assert.Throws<DomainException>(() => Rules.Record(ValidRecord(), 999, new(2026, 11, 18)));
    }

    [Fact]
    public void RejectEmptyDescription()
    {
        Assert.Throws<DomainException>(() =>
            Rules.Record(ValidRecord() with { Description = " " }, 1000, new(2026, 11, 18))
        );
    }

    [Fact]
    public void ComponentMustBelongToVehicle()
    {
        Assert.Throws<DomainException>(() =>
            ComponentCatalog.ValidateLinks([Guid.NewGuid()], [Guid.NewGuid()])
        );
    }

    [Fact]
    public void ComponentMustBeDistinct()
    {
        var c = Guid.NewGuid();
        Assert.Throws<DomainException>(() => ComponentCatalog.ValidateLinks([c, c], [c]));
    }

    [Fact]
    public void MultipleValidComponentsAreAllowed()
    {
        var a = Guid.NewGuid();
        var b = Guid.NewGuid();
        ComponentCatalog.ValidateLinks([a, b], [a, b]);
    }
}
