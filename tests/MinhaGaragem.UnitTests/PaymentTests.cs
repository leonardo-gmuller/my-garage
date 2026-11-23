using MinhaGaragem.Application;
using MinhaGaragem.Domain;

namespace MinhaGaragem.UnitTests;

public sealed class PaymentTests
{
    private static RecordInput Input() =>
        new(
            new(2026, 10, 12),
            MaintenanceCategory.General,
            ServiceType.Replacement,
            "Peças e mão de obra",
            null,
            1000,
            null,
            null,
            [Guid.NewGuid()],
            [new("Peças de teste", 1, 2000)]
        );

    [Fact]
    public void IndependentChargesAddInterestOnceWithoutChangingPartsOrLabor()
    {
        var record = new MaintenanceRecord
        {
            Date = new(2026, 10, 12),
            LaborCost = 1000,
            Items =
            [
                new()
                {
                    Name = "Peças",
                    Quantity = 1,
                    UnitCost = 2000,
                },
            ],
            Payments =
            [
                new()
                {
                    Scope = PaymentScope.Parts,
                    Method = PaymentMethod.CreditCard,
                    Installments = 6,
                    TotalCharged = 2200,
                },
                new()
                {
                    Scope = PaymentScope.Labor,
                    Method = PaymentMethod.CreditCard,
                    Installments = 3,
                    TotalCharged = 1100,
                },
            ],
        };
        Assert.Equal(3000, record.BaseCost);
        Assert.Equal(300, record.InterestCost);
        Assert.Equal(3300, record.TotalCost);
        var result = FinancialSummary.Calculate([record], 2026);
        Assert.Equal(2000, result.Parts);
        Assert.Equal(1000, result.Labor);
        Assert.Equal(300, result.Interest);
        Assert.Equal(3300, result.Total);
        Assert.Equal(1, result.ServiceCount);
        var month = result.Monthly.Single(m => m.Month == "2026-10");
        Assert.Equal(month.Total, month.Parts + month.Labor + month.Unallocated + month.Interest);
        Assert.Equal(new decimal?[] { 200, 100 }, record.ToDto().Payments!.Select(p => p.Interest));
    }

    [Fact]
    public void ConfirmedCardTotalsKeepExactCents()
    {
        var record = new MaintenanceRecord
        {
            LaborCost = 1000,
            Items = [new() { UnitCost = 2000 }],
            Payments =
            [
                new()
                {
                    Scope = PaymentScope.Parts,
                    Installments = 10,
                    TotalCharged = 2500.40m,
                },
                new()
                {
                    Scope = PaymentScope.Labor,
                    Installments = 10,
                    TotalCharged = 1080.25m,
                },
            ],
        };
        Assert.Equal(
            500.40m,
            record.ToDto().Payments!.Single(p => p.Scope == PaymentScope.Parts).Interest
        );
        Assert.Equal(
            80.25m,
            record.ToDto().Payments!.Single(p => p.Scope == PaymentScope.Labor).Interest
        );
        Assert.Equal(580.65m, record.InterestCost);
        Assert.Equal(3580.65m, record.TotalCost);
    }

    [Fact]
    public void UnknownChargesRemainPendingInsteadOfInventingZeroInterest()
    {
        var record = new MaintenanceRecord
        {
            LaborCost = 1000,
            Payments = [new() { Scope = PaymentScope.Labor, Method = PaymentMethod.CreditCard }],
        };
        Assert.Equal(1000, record.TotalCost);
        Assert.Equal(1, record.PendingPaymentCount);
        var payment = Assert.Single(record.ToDto().Payments!);
        Assert.Null(payment.Interest);
        Assert.Null(payment.TotalCharged);
        Assert.Null(payment.Installments);
        Assert.Equal(1, FinancialSummary.Calculate([record], 2026).PendingPaymentCount);
    }

    [Fact]
    public void ClosedTotalCanHaveItsOwnChargeWithoutInventingABreakdown()
    {
        var record = new MaintenanceRecord
        {
            PackageTotal = 1000,
            Payments =
            [
                new()
                {
                    Scope = PaymentScope.Package,
                    Method = PaymentMethod.CreditCard,
                    TotalCharged = 1120,
                },
            ],
        };
        Assert.Equal(1120, record.TotalCost);
        Assert.Equal(120, record.InterestCost);
        Assert.Null(record.ToDto().PartsCost);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(0)]
    [InlineData(121)]
    public void InvalidInstallmentCountsAreRejected(int installments) =>
        Assert.Throws<DomainException>(() =>
            PaymentRules.Validate(
                Input() with
                {
                    Payments = [new(PaymentScope.Parts, PaymentMethod.CreditCard, installments)],
                }
            )
        );

    [Fact]
    public void DuplicateScopesAndIncompatiblePricingModesAreRejected()
    {
        Assert.Throws<DomainException>(() =>
            PaymentRules.Validate(
                Input() with
                {
                    Payments =
                    [
                        new(PaymentScope.Parts, PaymentMethod.CreditCard),
                        new(PaymentScope.Parts, PaymentMethod.CreditCard),
                    ],
                }
            )
        );
        Assert.Throws<DomainException>(() =>
            PaymentRules.Validate(
                Input() with
                {
                    Payments = [new(PaymentScope.Package, PaymentMethod.CreditCard)],
                }
            )
        );
        Assert.Throws<DomainException>(() =>
            Rules.Record(
                Input() with
                {
                    PackageTotal = 1000,
                    LaborCost = 0,
                    Items = [],
                    Payments = [new(PaymentScope.Labor, PaymentMethod.CreditCard)],
                },
                null,
                new(2026, 11, 19)
            )
        );
    }

    [Theory]
    [InlineData(1999)]
    [InlineData(2000.001)]
    [InlineData(100000001)]
    public void InvalidChargedTotalsAreRejected(decimal total) =>
        Assert.Throws<DomainException>(() =>
            PaymentRules.Validate(
                Input() with
                {
                    Payments = [new(PaymentScope.Parts, PaymentMethod.CreditCard, 6, total)],
                }
            )
        );

    [Fact]
    public void ExactPrincipalMeansExplicitZeroInterestAndPendingInformationIsAllowed()
    {
        PaymentRules.Validate(
            Input() with
            {
                Payments =
                [
                    new(PaymentScope.Parts, PaymentMethod.CreditCard, 6, 2000),
                    new(PaymentScope.Labor, PaymentMethod.CreditCard),
                ],
            }
        );
        Assert.Throws<DomainException>(() =>
            PaymentRules.Validate(
                Input() with
                {
                    Payments = [new(PaymentScope.Parts, (PaymentMethod)999)],
                }
            )
        );
    }
}
