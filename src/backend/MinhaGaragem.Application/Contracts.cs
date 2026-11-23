using Microsoft.EntityFrameworkCore;
using MinhaGaragem.Domain;

namespace MinhaGaragem.Application;

public interface ICurrentUser
{
    Guid Id { get; }
}

public interface IGarageDb
{
    DbSet<Mechanic> Mechanics { get; }
    DbSet<Vehicle> Vehicles { get; }
    DbSet<VehicleComponent> Components { get; }
    DbSet<VehicleSubcomponent> Subcomponents { get; }
    DbSet<MaintenanceRecordSubcomponent> RecordSubcomponents { get; }
    DbSet<MaintenanceRecord> Records { get; }
    DbSet<MaintenanceItem> Items { get; }
    DbSet<MaintenancePayment> Payments { get; }
    DbSet<MaintenanceSchedule> Schedules { get; }
    DbSet<MaintenanceAttachment> Attachments { get; }
    DbSet<VehicleMileageHistory> MileageHistory { get; }
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}

public sealed record VehicleInput(
    string Manufacturer,
    string Model,
    int ManufactureYear,
    int ModelYear,
    string? Version,
    string? Color,
    int? Mileage,
    string? Nickname,
    string? Plate,
    string? ColorHex = null,
    PaintFinish? PaintFinish = null
);

public sealed record PaintInput(string Color, string ColorHex, PaintFinish? PaintFinish);

public sealed record MileageInput(int Mileage);

public sealed record ItemInput(string Name, int Quantity, decimal UnitCost);

public sealed record RecordInput(
    DateOnly Date,
    MaintenanceCategory Category,
    ServiceType ServiceType,
    string Description,
    int? Mileage,
    decimal LaborCost,
    string? Workshop,
    string? Notes,
    List<Guid> ComponentIds,
    List<ItemInput> Items,
    List<Guid>? SubcomponentIds = null,
    Guid? PrimaryComponentId = null,
    Guid? MechanicId = null,
    decimal? PackageTotal = null,
    List<PaymentInput>? Payments = null
);

public sealed record PaymentInput(
    PaymentScope Scope,
    PaymentMethod Method,
    int? Installments = null,
    decimal? TotalCharged = null
);

public sealed record PaymentDto(
    Guid Id,
    PaymentScope Scope,
    PaymentMethod Method,
    int? Installments,
    decimal Principal,
    decimal? TotalCharged,
    decimal? Interest
);

public sealed record ScheduleInput(
    Guid ComponentId,
    string Description,
    DateOnly? DueDate,
    int? DueMileage,
    SchedulePriority Priority,
    bool Completed,
    Guid? SubcomponentId = null
);

public sealed record Page<T>(IReadOnlyList<T> Items, int Total, int PageNumber, int PageSize);

public sealed record SubcomponentDto(Guid Id, Guid ComponentId, string Key, string Name);

public sealed record ComponentDto(
    Guid Id,
    string Key,
    string Name,
    IReadOnlyList<SubcomponentDto>? Subcomponents = null
);

public sealed record VehicleDto(
    Guid Id,
    string Manufacturer,
    string Model,
    int ManufactureYear,
    int ModelYear,
    string? Version,
    string? Color,
    int? Mileage,
    string? Nickname,
    string? Plate,
    string? PhotoUrl,
    string? ColorHex = null,
    PaintFinish? PaintFinish = null
);

public sealed record ItemDto(Guid Id, string Name, int Quantity, decimal? UnitCost, decimal? Total);

public sealed record AttachmentDto(
    Guid Id,
    string FileName,
    string ContentType,
    long Size,
    string Url
);

public sealed record RecordDto(
    Guid Id,
    Guid VehicleId,
    DateOnly Date,
    MaintenanceCategory Category,
    ServiceType ServiceType,
    string Description,
    int? Mileage,
    decimal? LaborCost,
    decimal? PartsCost,
    decimal TotalCost,
    string? Workshop,
    string? Notes,
    IReadOnlyList<ComponentDto> Components,
    IReadOnlyList<ItemDto> Items,
    IReadOnlyList<AttachmentDto> Attachments,
    IReadOnlyList<SubcomponentDto> Subcomponents,
    Guid? PrimaryComponentId,
    Guid? MechanicId = null,
    string? MechanicName = null,
    decimal? PackageTotal = null,
    IReadOnlyList<PaymentDto>? Payments = null,
    decimal BaseCost = 0,
    decimal InterestCost = 0,
    int PendingPaymentCount = 0
);

public sealed record ScheduleDto(
    Guid Id,
    Guid ComponentId,
    string ComponentName,
    string Description,
    DateOnly? DueDate,
    int? DueMileage,
    SchedulePriority Priority,
    bool Completed,
    ScheduleState State,
    Guid? SubcomponentId = null,
    string? SubcomponentName = null
);

public sealed record ComponentSummaryDto(
    int ServiceCount,
    decimal TotalCost,
    RecordDto? LastService,
    IReadOnlyList<RecordDto> Recent,
    ScheduleDto? NextMaintenance,
    int PendingPaymentCount = 0
);

public sealed record MonthlyExpense(
    string Month,
    decimal Parts,
    decimal Labor,
    decimal Total,
    decimal Unallocated = 0,
    decimal Interest = 0
);

public sealed record CategoryExpense(MaintenanceCategory Category, decimal Total);

public sealed record AnnualExpense(int Year, decimal Total);

public sealed record IndicatorsDto(
    decimal Total,
    decimal Parts,
    decimal Labor,
    int ServiceCount,
    DateOnly? LastMaintenance,
    IReadOnlyList<MonthlyExpense> Monthly,
    IReadOnlyList<AnnualExpense> Annual,
    IReadOnlyList<CategoryExpense> Categories,
    IReadOnlyList<RecordDto> MostExpensive,
    decimal Unallocated = 0,
    decimal Interest = 0,
    int PendingPaymentCount = 0
);

public static class Mapping
{
    public static VehicleDto ToDto(this Vehicle v) =>
        new(
            v.Id,
            v.Manufacturer,
            v.Model,
            v.ManufactureYear,
            v.ModelYear,
            v.Version,
            v.Color,
            v.Mileage,
            v.Nickname,
            v.Plate,
            v.PhotoKey is null ? null : $"/api/vehicles/{v.Id}/photo",
            v.ColorHex,
            v.PaintFinish
        );

    public static SubcomponentDto ToDto(this VehicleSubcomponent s) =>
        new(s.Id, s.VehicleComponentId, s.Key, s.Name);

    public static RecordDto ToDto(this MaintenanceRecord r) =>
        new(
            r.Id,
            r.VehicleId,
            r.Date,
            r.Category,
            r.ServiceType,
            r.Description,
            r.Mileage,
            r.PackageTotal.HasValue ? null : r.LaborCost,
            r.PackageTotal.HasValue ? null : r.PartsCost,
            r.TotalCost,
            r.Workshop,
            r.Notes,
            r.Components.Select(x => new ComponentDto(
                    x.VehicleComponentId,
                    x.Component.Key,
                    x.Component.Name
                ))
                .ToArray(),
            r.Items.Select(x => new ItemDto(
                    x.Id,
                    x.Name,
                    x.Quantity,
                    r.PackageTotal.HasValue ? null : x.UnitCost,
                    r.PackageTotal.HasValue ? null : x.Total
                ))
                .ToArray(),
            r.Attachments.Select(x => new AttachmentDto(
                    x.Id,
                    x.FileName,
                    x.ContentType,
                    x.Size,
                    $"/api/vehicles/{r.VehicleId}/records/{r.Id}/attachments/{x.Id}"
                ))
                .ToArray(),
            r.Subcomponents.Select(x => x.Subcomponent.ToDto()).ToArray(),
            r.PrimaryComponentId,
            r.MechanicId,
            r.MechanicName,
            r.PackageTotal,
            r.Payments.OrderBy(p => p.Scope)
                .Select(p => new PaymentDto(
                    p.Id,
                    p.Scope,
                    p.Method,
                    p.Installments,
                    r.PrincipalFor(p.Scope),
                    p.TotalCharged,
                    p.TotalCharged - r.PrincipalFor(p.Scope)
                ))
                .ToArray(),
            r.BaseCost,
            r.InterestCost,
            r.PendingPaymentCount
        );
}

public static class Rules
{
    public static void Paint(string? color, string? hex, PaintFinish? finish)
    {
        Text(color, "Cor", 80);
        if (
            hex is not null
            && !System.Text.RegularExpressions.Regex.IsMatch(hex, "^#[0-9a-fA-F]{6}$")
        )
            throw new DomainException("Informe uma cor hexadecimal no formato #RRGGBB.");
        if (finish.HasValue && !Enum.IsDefined(finish.Value))
            throw new DomainException("Acabamento inválido.");
        if (hex is not null && string.IsNullOrWhiteSpace(color))
            throw new DomainException("Informe o nome da cor.");
    }

    public static void Text(string? value, string field, int max, bool required = false)
    {
        if ((required && string.IsNullOrWhiteSpace(value)) || value?.Length > max)
            throw new DomainException($"{field}: preencha um texto de até {max} caracteres.");
    }

    public static void Money(decimal value)
    {
        if (value < 0 || value > 100_000_000 || decimal.Round(value, 2) != value)
            throw new DomainException(
                "Informe valores de 0 a 100 milhões, com até duas casas decimais."
            );
    }

    public static void Item(ItemInput i)
    {
        Text(i.Name, "Peça", 160, true);
        Money(i.UnitCost);
        if (i.Quantity is < 1 or > 10000)
            throw new DomainException("Quantidade deve ser de 1 a 10.000.");
    }

    public static void Record(RecordInput i, int? mileage, DateOnly today)
    {
        Text(i.Description, "Descrição", 300, true);
        Text(i.Workshop, "Oficina", 200);
        Text(i.Notes, "Observações", 4000);
        if (i.Date > today || i.Date.Year < 1900)
            throw new DomainException("A data do serviço deve estar entre 1900 e hoje.");
        if (i.Mileage < 0 || i.Mileage > 10_000_000 || (mileage.HasValue && i.Mileage > mileage))
            throw new DomainException(
                "A quilometragem do serviço não pode exceder o hodômetro atual."
            );
        if (!Enum.IsDefined(i.Category) || !Enum.IsDefined(i.ServiceType))
            throw new DomainException("Categoria ou tipo inválido.");
        Money(i.LaborCost);
        if (i.Items is null || i.Items.Count > 100 || i.ComponentIds is null)
            throw new DomainException("Informe até 100 peças e os componentes.");
        foreach (var item in i.Items)
            Item(item);
        if (i.PackageTotal is decimal total)
        {
            Money(total);
            if (i.LaborCost != 0 || i.Items.Any(item => item.UnitCost != 0))
                throw new DomainException(
                    "No valor total fechado, informe apenas o total cobrado, sem dividir os custos de peças e mão de obra."
                );
        }
        PaymentRules.Validate(i);
    }
}
