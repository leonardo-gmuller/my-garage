namespace MinhaGaragem.Domain;

public enum PaymentScope
{
    Parts,
    Labor,
    Package,
}

public enum PaymentMethod
{
    CreditCard,
    DebitCard,
    Pix,
    Cash,
    Other,
}

public sealed class MaintenancePayment
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid MaintenanceRecordId { get; set; }
    public MaintenanceRecord Record { get; set; } = null!;
    public PaymentScope Scope { get; set; }
    public PaymentMethod Method { get; set; }
    public int? Installments { get; set; }

    // Null means the final charge, including any interest, is still unknown.
    public decimal? TotalCharged { get; set; }
}
