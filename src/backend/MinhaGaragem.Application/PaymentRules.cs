using MinhaGaragem.Domain;

namespace MinhaGaragem.Application;

public static class PaymentRules
{
    public static void Validate(RecordInput input)
    {
        var payments = input.Payments ?? [];
        if (
            payments.Count > 2
            || payments.Any(p => p is null)
            || payments.Select(p => p.Scope).Distinct().Count() != payments.Count
        )
            throw new DomainException(
                "Informe um pagamento por cobrança: peças e mão de obra, ou o total fechado."
            );
        var parts = input.Items.Sum(i => i.Quantity * i.UnitCost);
        foreach (var payment in payments)
        {
            if (!Enum.IsDefined(payment.Scope) || !Enum.IsDefined(payment.Method))
                throw new DomainException("Tipo de cobrança ou forma de pagamento inválida.");
            if ((payment.Scope == PaymentScope.Package) != input.PackageTotal.HasValue)
                throw new DomainException(
                    "Revise os pagamentos: peças e mão de obra usam valores detalhados; o pagamento único usa valor total fechado."
                );
            if (payment.Installments is < 1 or > 120)
                throw new DomainException(
                    "Informe de 1 a 120 parcelas, ou deixe a quantidade pendente."
                );
            var principal = payment.Scope switch
            {
                PaymentScope.Parts => parts,
                PaymentScope.Labor => input.LaborCost,
                _ => input.PackageTotal!.Value,
            };
            if (payment.TotalCharged is decimal charged)
            {
                Rules.Money(charged);
                if (charged < principal)
                    throw new DomainException(
                        "O total da cobrança com juros não pode ser menor que o valor original. Registre descontos no valor das peças ou da mão de obra."
                    );
            }
        }
    }
}
