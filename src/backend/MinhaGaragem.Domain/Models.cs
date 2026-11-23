namespace MinhaGaragem.Domain;

public sealed class DomainException(string message) : Exception(message);

public enum ServiceType
{
    Preventive,
    Corrective,
    Inspection,
    Replacement,
}

public enum MaintenanceCategory
{
    Engine,
    Electrical,
    Tires,
    Brakes,
    Suspension,
    Cooling,
    Transmission,
    Exhaust,
    Body,
    General,
}

public enum SchedulePriority
{
    Low,
    Normal,
    High,
}

public enum ScheduleState
{
    Current,
    DueSoon,
    Overdue,
    Completed,
}

public enum PaintFinish
{
    Solid,
    Metallic,
    Pearlescent,
    Matte,
}

public sealed class Vehicle
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public string Manufacturer { get; set; } = "";
    public string Model { get; set; } = "";
    public int ManufactureYear { get; set; }
    public int ModelYear { get; set; }
    public string? Version { get; set; }
    public string? Color { get; set; }
    public string? ColorHex { get; set; }
    public PaintFinish? PaintFinish { get; set; }
    public int? Mileage { get; set; }
    public string? Nickname { get; set; }
    public string? Plate { get; set; }
    public string? PhotoKey { get; set; }
    public string? PhotoContentType { get; set; }
    public Guid ConcurrencyStamp { get; set; } = Guid.NewGuid();
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public void UpdateMileage(int mileage)
    {
        if (mileage < 0 || mileage > 10_000_000)
            throw new DomainException("Quilometragem inválida.");
        if (Mileage.HasValue && mileage <= Mileage)
            throw new DomainException("A nova quilometragem deve ser maior que a atual.");
        Mileage = mileage;
    }
}

public sealed class VehicleComponent
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid VehicleId { get; set; }
    public Vehicle Vehicle { get; set; } = null!;
    public string Key { get; set; } = "";
    public string Name { get; set; } = "";
    public List<VehicleSubcomponent> Subcomponents { get; set; } = [];
}

public sealed class VehicleSubcomponent
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid VehicleId { get; set; }
    public Guid VehicleComponentId { get; set; }
    public VehicleComponent Component { get; set; } = null!;
    public string Key { get; set; } = "";
    public string Name { get; set; } = "";
}

public static class ComponentCatalog
{
    public static readonly (string Key, string Name)[] All =
    [
        ("engine", "Motor"),
        ("battery", "Bateria"),
        ("wheels", "Pneus e rodas"),
        ("brakes", "Freios"),
        ("suspension", "Suspensão"),
        ("lights", "Faróis"),
        ("cooling", "Arrefecimento"),
        ("transmission", "Transmissão"),
        ("exhaust", "Escapamento"),
        ("body", "Carroceria"),
    ];

    public static void ValidateLinks(IEnumerable<Guid> requested, IEnumerable<Guid> allowed)
    {
        var ids = requested.ToArray();
        if (ids.Length == 0 || ids.Length != ids.Distinct().Count() || ids.Except(allowed).Any())
            throw new DomainException("Selecione componentes distintos deste veículo.");
    }
}

public sealed class MaintenanceRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid VehicleId { get; set; }
    public Vehicle Vehicle { get; set; } = null!;
    public Guid? PrimaryComponentId { get; set; }
    public VehicleComponent? PrimaryComponent { get; set; }
    public DateOnly Date { get; set; }
    public MaintenanceCategory Category { get; set; }
    public ServiceType ServiceType { get; set; }
    public string Description { get; set; } = "";
    public int? Mileage { get; set; }
    public decimal LaborCost { get; set; }
    public decimal? PackageTotal { get; set; }
    public string? Workshop { get; set; }
    public Guid? MechanicId { get; set; }
    public Mechanic? Mechanic { get; set; }
    public string? MechanicName { get; set; }
    public string? Notes { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public List<MaintenanceItem> Items { get; set; } = [];
    public List<MaintenanceRecordComponent> Components { get; set; } = [];
    public List<MaintenanceRecordSubcomponent> Subcomponents { get; set; } = [];
    public List<MaintenanceAttachment> Attachments { get; set; } = [];
    public List<MaintenancePayment> Payments { get; set; } = [];
    public decimal PartsCost => PackageTotal.HasValue ? 0 : Items.Sum(x => x.Total);
    public decimal BaseCost => PackageTotal ?? PartsCost + LaborCost;

    public decimal PrincipalFor(PaymentScope scope) =>
        scope switch
        {
            PaymentScope.Parts => PartsCost,
            PaymentScope.Labor => LaborCost,
            PaymentScope.Package => PackageTotal ?? 0,
            _ => 0,
        };

    public decimal InterestCost =>
        Payments
            .Where(p => p.TotalCharged.HasValue)
            .Sum(p => p.TotalCharged!.Value - PrincipalFor(p.Scope));
    public int PendingPaymentCount => Payments.Count(p => !p.TotalCharged.HasValue);
    public decimal TotalCost => BaseCost + InterestCost;
}

public sealed class MaintenanceRecordSubcomponent
{
    public Guid MaintenanceRecordId { get; set; }
    public Guid VehicleSubcomponentId { get; set; }
    public Guid VehicleComponentId { get; set; }
    public Guid VehicleId { get; set; }
    public MaintenanceRecordComponent ParentLink { get; set; } = null!;
    public MaintenanceRecord Record { get; set; } = null!;
    public VehicleSubcomponent Subcomponent { get; set; } = null!;
}

public sealed class MaintenanceRecordComponent
{
    public Guid MaintenanceRecordId { get; set; }
    public Guid VehicleComponentId { get; set; }
    public Guid VehicleId { get; set; }
    public MaintenanceRecord Record { get; set; } = null!;
    public VehicleComponent Component { get; set; } = null!;
}

public sealed class MaintenanceItem
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid MaintenanceRecordId { get; set; }
    public MaintenanceRecord Record { get; set; } = null!;
    public string Name { get; set; } = "";
    public int Quantity { get; set; } = 1;
    public decimal UnitCost { get; set; }
    public decimal Total => Quantity * UnitCost;
}

public sealed class MaintenanceSchedule
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid VehicleId { get; set; }
    public Vehicle Vehicle { get; set; } = null!;
    public Guid VehicleComponentId { get; set; }
    public VehicleComponent Component { get; set; } = null!;
    public Guid? VehicleSubcomponentId { get; set; }
    public VehicleSubcomponent? Subcomponent { get; set; }
    public string Description { get; set; } = "";
    public DateOnly? DueDate { get; set; }
    public int? DueMileage { get; set; }
    public SchedulePriority Priority { get; set; }
    public bool Completed { get; set; }

    public ScheduleState State(DateOnly today, int? currentMileage)
    {
        if (Completed)
            return ScheduleState.Completed;
        if (DueDate < today || (currentMileage.HasValue && DueMileage <= currentMileage))
            return ScheduleState.Overdue;
        if (
            DueDate <= today.AddDays(30)
            || (currentMileage.HasValue && DueMileage <= (long)currentMileage + 1000)
        )
            return ScheduleState.DueSoon;
        return ScheduleState.Current;
    }
}

public sealed class MaintenanceAttachment
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid MaintenanceRecordId { get; set; }
    public MaintenanceRecord Record { get; set; } = null!;
    public string FileName { get; set; } = "";
    public string StorageKey { get; set; } = "";
    public string ContentType { get; set; } = "";
    public long Size { get; set; }
}

public sealed class VehicleMileageHistory
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid VehicleId { get; set; }
    public Vehicle Vehicle { get; set; } = null!;
    public int Mileage { get; set; }
    public DateTimeOffset RecordedAt { get; set; } = DateTimeOffset.UtcNow;
}
