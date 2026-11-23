using MinhaGaragem.Application;
using MinhaGaragem.Domain;

namespace MinhaGaragem.UnitTests;

public sealed class MechanicRulesTests
{
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void RequiresName(string name) =>
        Assert.Throws<DomainException>(() => MechanicRules.Validate(new(name)));

    [Theory]
    [InlineData("1199")]
    [InlineData("ligar amanhã")]
    [InlineData("1234567890123456")]
    [InlineData("11#99998888")]
    public void RejectsInvalidPhone(string phone) =>
        Assert.Throws<DomainException>(() => MechanicRules.Validate(new("João", Phone: phone)));

    [Theory]
    [InlineData("+55 (11) 99999-8888")]
    [InlineData("(11) 3333-4444")]
    [InlineData(null)]
    public void AcceptsFormattedOrUnknownPhone(string? phone) =>
        MechanicRules.Validate(new("João", Phone: phone));

    [Theory]
    [InlineData("sem-email")]
    [InlineData("João <joao@example.test>")]
    public void RejectsInvalidEmail(string email) =>
        Assert.Throws<DomainException>(() => MechanicRules.Validate(new("João", Email: email)));

    [Fact]
    public void OptionalDetailsAreNotRequired() => MechanicRules.Validate(new("João"));

    [Fact]
    public void AcceptsDetailsAndRejectsOversizedNotes()
    {
        MechanicRules.Validate(
            new("João", Email: "joao@example.test", Notes: new string('x', 4000))
        );
        Assert.Throws<DomainException>(() =>
            MechanicRules.Validate(new("João", Notes: new string('x', 4001)))
        );
    }
}
