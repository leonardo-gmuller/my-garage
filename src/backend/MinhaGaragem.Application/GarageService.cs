using Microsoft.EntityFrameworkCore;
using MinhaGaragem.Domain;

namespace MinhaGaragem.Application;

public sealed class NotFoundException : Exception;

public sealed class GarageService(IGarageDb db, ICurrentUser user, TimeProvider clock)
{
    public DateOnly Today =>
        DateOnly.FromDateTime(
            TimeZoneInfo
                .ConvertTime(
                    clock.GetUtcNow(),
                    TimeZoneInfo.FindSystemTimeZoneById("America/Sao_Paulo")
                )
                .DateTime
        );

    public async Task<Vehicle> Vehicle(Guid id, CancellationToken ct) =>
        await db.Vehicles.SingleOrDefaultAsync(v => v.Id == id && v.UserId == user.Id, ct)
        ?? throw new NotFoundException();

    public async Task<Page<VehicleDto>> Vehicles(int page, int size, CancellationToken ct)
    {
        Pagination(ref page, ref size);
        var query = db.Vehicles.AsNoTracking().Where(v => v.UserId == user.Id);
        var total = await query.CountAsync(ct);
        var rows = await query
            .OrderBy(v => v.CreatedAt)
            .ThenBy(v => v.Id)
            .Skip((page - 1) * size)
            .Take(size)
            .ToListAsync(ct);
        return new(rows.Select(v => v.ToDto()).ToArray(), total, page, size);
    }

    public async Task<VehicleDto> SaveVehicle(Guid? id, VehicleInput input, CancellationToken ct)
    {
        Rules.Text(input.Manufacturer, "Fabricante", 80, true);
        Rules.Text(input.Model, "Modelo", 100, true);
        Rules.Text(input.Version, "Versão", 120);
        Rules.Text(input.Color, "Cor", 80);
        Rules.Text(input.Nickname, "Apelido", 80);
        Rules.Text(input.Plate, "Placa", 12);
        Rules.Paint(input.Color, input.ColorHex, input.PaintFinish);
        if (
            input.ManufactureYear < 1900
            || input.ManufactureYear > Today.Year + 1
            || input.ModelYear < 1900
            || input.ModelYear > Today.Year + 1
            || input.Mileage < 0
            || input.Mileage > 10_000_000
        )
            throw new DomainException("Ano ou quilometragem inválidos.");
        var v = id.HasValue ? await Vehicle(id.Value, ct) : new Vehicle { UserId = user.Id };
        v.Manufacturer = input.Manufacturer.Trim();
        v.Model = input.Model.Trim();
        v.ManufactureYear = input.ManufactureYear;
        v.ModelYear = input.ModelYear;
        v.Version = input.Version?.Trim();
        v.Color = input.Color?.Trim();
        v.Nickname = input.Nickname?.Trim();
        v.Plate = input.Plate?.Trim().ToUpperInvariant();
        v.ColorHex = input.ColorHex?.ToUpperInvariant();
        v.PaintFinish = input.PaintFinish;
        if (id is null)
        {
            db.Vehicles.Add(v);
            foreach (var c in ComponentCatalog.All)
                db.Components.Add(SubcomponentCatalog.CreateComponent(v.Id, c.Key, c.Name));
        }
        if (input.Mileage.HasValue && input.Mileage != v.Mileage)
        {
            v.UpdateMileage(input.Mileage.Value);
            db.MileageHistory.Add(new() { VehicleId = v.Id, Mileage = input.Mileage.Value });
        }
        await db.SaveChangesAsync(ct);
        return v.ToDto();
    }

    public async Task UpdateMileage(Guid vehicleId, int value, CancellationToken ct)
    {
        var v = await Vehicle(vehicleId, ct);
        v.UpdateMileage(value);
        db.MileageHistory.Add(new() { VehicleId = v.Id, Mileage = value });
        await db.SaveChangesAsync(ct);
    }

    public async Task<VehicleDto> SavePaint(Guid vehicleId, PaintInput input, CancellationToken ct)
    {
        Rules.Text(input.ColorHex, "Cor hexadecimal", 7, true);
        Rules.Text(input.Color, "Cor", 80, true);
        Rules.Paint(input.Color, input.ColorHex, input.PaintFinish);
        var v = await Vehicle(vehicleId, ct);
        v.Color = input.Color.Trim();
        v.ColorHex = input.ColorHex.ToUpperInvariant();
        v.PaintFinish = input.PaintFinish;
        await db.SaveChangesAsync(ct);
        return v.ToDto();
    }

    public IQueryable<MaintenanceRecord> RecordQuery(Guid? vehicleId = null) =>
        db
            .Records.Where(r => vehicleId == null || r.VehicleId == vehicleId)
            .Include(r => r.Items)
            .Include(r => r.Payments)
            .Include(r => r.Components)
                .ThenInclude(c => c.Component)
            .Include(r => r.Subcomponents)
                .ThenInclude(c => c.Subcomponent)
            .Include(r => r.Attachments);

    public async Task<MaintenanceRecord> Record(Guid vehicleId, Guid recordId, CancellationToken ct)
    {
        await Vehicle(vehicleId, ct);
        return await RecordQuery(vehicleId).SingleOrDefaultAsync(r => r.Id == recordId, ct)
            ?? throw new NotFoundException();
    }

    public async Task<Page<RecordDto>> Records(
        Guid vehicleId,
        int page,
        int size,
        DateOnly? from,
        DateOnly? to,
        MaintenanceCategory? category,
        Guid? component,
        CancellationToken ct,
        Guid? subcomponent = null
    )
    {
        await Vehicle(vehicleId, ct);
        Pagination(ref page, ref size);
        if (from > to)
            throw new DomainException("O início do período deve vir antes do fim.");
        var q = RecordQuery(vehicleId).AsNoTracking();
        if (from.HasValue)
            q = q.Where(r => r.Date >= from);
        if (to.HasValue)
            q = q.Where(r => r.Date <= to);
        if (category.HasValue)
            q = q.Where(r => r.Category == category);
        if (component.HasValue)
            q = q.Where(r => r.Components.Any(c => c.VehicleComponentId == component));
        if (subcomponent.HasValue)
            q = q.Where(r => r.Subcomponents.Any(c => c.VehicleSubcomponentId == subcomponent));
        var total = await q.CountAsync(ct);
        var rows = await q.OrderByDescending(r => r.Date)
            .ThenByDescending(r => r.CreatedAt)
            .ThenBy(r => r.Id)
            .Skip((page - 1) * size)
            .Take(size)
            .ToListAsync(ct);
        return new(rows.Select(r => r.ToDto()).ToArray(), total, page, size);
    }

    public async Task<RecordDto> SaveRecord(
        Guid vehicleId,
        Guid? id,
        RecordInput input,
        CancellationToken ct
    )
    {
        var v = await Vehicle(vehicleId, ct);
        var existingRecord = id.HasValue ? await Record(vehicleId, id.Value, ct) : null;
        if (input.Payments is null && existingRecord is not null)
            input = input with
            {
                Payments = existingRecord
                    .Payments.Select(p => new PaymentInput(
                        p.Scope,
                        p.Method,
                        p.Installments,
                        p.TotalCharged
                    ))
                    .ToList(),
            };
        Rules.Record(input, v.Mileage, Today);
        ComponentCatalog.ValidateLinks(
            input.ComponentIds,
            await db
                .Components.Where(c => c.VehicleId == vehicleId)
                .Select(c => c.Id)
                .ToArrayAsync(ct)
        );
        var subIds = input.SubcomponentIds ?? [];
        var subs = await db
            .Subcomponents.Where(s => s.VehicleId == vehicleId && subIds.Contains(s.Id))
            .ToArrayAsync(ct);
        if (
            subIds.Count > 100
            || subIds.Count != subIds.Distinct().Count()
            || subs.Length != subIds.Count
            || subs.Any(s => !input.ComponentIds.Contains(s.VehicleComponentId))
        )
            throw new DomainException(
                "Selecione subcomponentes distintos dos componentes envolvidos neste veículo."
            );
        var primary = input.PrimaryComponentId ?? input.ComponentIds[0];
        if (!input.ComponentIds.Contains(primary))
            throw new DomainException(
                "O componente principal deve estar entre os componentes envolvidos."
            );
        var r = existingRecord ?? new MaintenanceRecord { VehicleId = vehicleId };
        Mechanic? mechanic = null;
        if (input.MechanicId.HasValue)
        {
            mechanic = await db.Mechanics.SingleOrDefaultAsync(
                m => m.Id == input.MechanicId && m.UserId == user.Id,
                ct
            ) ?? throw new DomainException("Selecione um mecânico da sua agenda.");
            if (mechanic.IsArchived && r.MechanicId != mechanic.Id)
                throw new DomainException(
                    "Este mecânico está arquivado. Restaure o contato ou selecione outro mecânico."
                );
        }
        var sameMechanic = mechanic is not null && r.MechanicId == mechanic.Id;
        var workshop = MechanicRules.Optional(input.Workshop);
        if (workshop is null && mechanic is not null)
            workshop = sameMechanic ? r.Workshop ?? mechanic.Workshop : mechanic.Workshop;
        r.MechanicName = sameMechanic ? r.MechanicName ?? mechanic!.Name : mechanic?.Name;
        r.MechanicId = mechanic?.Id;
        r.Date = input.Date;
        r.Category = input.Category;
        r.ServiceType = input.ServiceType;
        r.Description = input.Description.Trim();
        r.Mileage = input.Mileage;
        r.LaborCost = input.LaborCost;
        r.PackageTotal = input.PackageTotal;
        r.Workshop = workshop;
        r.Notes = input.Notes?.Trim();
        r.PrimaryComponentId = primary;
        if (id is null)
            db.Records.Add(r);
        db.Items.RemoveRange(r.Items);
        r.Items.Clear();
        foreach (var item in input.Items)
            db.Items.Add(
                new MaintenanceItem
                {
                    Record = r,
                    MaintenanceRecordId = r.Id,
                    Name = item.Name.Trim(),
                    Quantity = item.Quantity,
                    UnitCost = item.UnitCost,
                }
            );
        var payments = input.Payments ?? [];
        var removedPayments = r
            .Payments.Where(p => !payments.Any(x => x.Scope == p.Scope))
            .ToArray();
        db.Payments.RemoveRange(removedPayments);
        r.Payments.RemoveAll(p => removedPayments.Contains(p));
        foreach (var payment in payments)
        {
            var entity = r.Payments.SingleOrDefault(p => p.Scope == payment.Scope);
            if (entity is null)
            {
                entity = new MaintenancePayment
                {
                    Record = r,
                    MaintenanceRecordId = r.Id,
                    Scope = payment.Scope,
                };
                db.Payments.Add(entity);
            }
            entity.Method = payment.Method;
            entity.Installments = payment.Installments;
            entity.TotalCharged = payment.TotalCharged;
        }
        // Keep existing joins to avoid tracking two instances with the same composite key.
        var removed = r
            .Subcomponents.Where(s => !subIds.Contains(s.VehicleSubcomponentId))
            .ToArray();
        db.RecordSubcomponents.RemoveRange(removed);
        r.Subcomponents.RemoveAll(s => !subIds.Contains(s.VehicleSubcomponentId));
        r.Components.RemoveAll(c => !input.ComponentIds.Contains(c.VehicleComponentId));
        foreach (
            var cid in input.ComponentIds.Except(r.Components.Select(c => c.VehicleComponentId))
        )
            r.Components.Add(new() { VehicleId = vehicleId, VehicleComponentId = cid });
        foreach (
            var sub in subs.Where(s =>
                r.Subcomponents.All(link => link.VehicleSubcomponentId != s.Id)
            )
        )
            r.Subcomponents.Add(
                new()
                {
                    VehicleId = vehicleId,
                    VehicleComponentId = sub.VehicleComponentId,
                    VehicleSubcomponentId = sub.Id,
                }
            );
        await db.SaveChangesAsync(ct);
        return (await Record(vehicleId, r.Id, ct)).ToDto();
    }

    public async Task<ScheduleDto> SaveSchedule(
        Guid vehicleId,
        Guid? id,
        ScheduleInput input,
        CancellationToken ct
    )
    {
        var v = await Vehicle(vehicleId, ct);
        Rules.Text(input.Description, "Descrição", 300, true);
        if (
            (input.DueDate is null && input.DueMileage is null)
            || input.DueMileage < 0
            || input.DueMileage > 10_000_000
            || input.DueDate?.Year < 1900
            || !Enum.IsDefined(input.Priority)
        )
            throw new DomainException("Informe uma data ou quilometragem válida e uma prioridade.");
        var c = await db.Components.SingleOrDefaultAsync(
            c => c.Id == input.ComponentId && c.VehicleId == vehicleId,
            ct
        ) ?? throw new DomainException("Componente inválido para este veículo.");
        VehicleSubcomponent? sub = null;
        if (input.SubcomponentId.HasValue)
            sub = await db.Subcomponents.SingleOrDefaultAsync(
                s =>
                    s.Id == input.SubcomponentId
                    && s.VehicleId == vehicleId
                    && s.VehicleComponentId == c.Id,
                ct
            ) ?? throw new DomainException("Subcomponente inválido para este componente.");
        var s = id.HasValue
            ? await db.Schedules.SingleOrDefaultAsync(
                s => s.Id == id && s.VehicleId == vehicleId,
                ct
            ) ?? throw new NotFoundException()
            : new MaintenanceSchedule { VehicleId = vehicleId };
        s.VehicleComponentId = c.Id;
        s.Component = c;
        s.Description = input.Description.Trim();
        s.DueDate = input.DueDate;
        s.DueMileage = input.DueMileage;
        s.Priority = input.Priority;
        s.Completed = input.Completed;
        s.VehicleSubcomponentId = sub?.Id;
        s.Subcomponent = sub;
        if (id is null)
            db.Schedules.Add(s);
        await db.SaveChangesAsync(ct);
        return ScheduleDto(s, v.Mileage);
    }

    public ScheduleDto ScheduleDto(MaintenanceSchedule s, int? mileage) =>
        new(
            s.Id,
            s.VehicleComponentId,
            s.Component.Name,
            s.Description,
            s.DueDate,
            s.DueMileage,
            s.Priority,
            s.Completed,
            s.State(Today, mileage),
            s.VehicleSubcomponentId,
            s.Subcomponent?.Name
        );

    public async Task<ComponentSummaryDto> ComponentSummary(
        Guid vehicleId,
        Guid componentId,
        Guid? subcomponentId,
        CancellationToken ct
    )
    {
        var v = await Vehicle(vehicleId, ct);
        if (!await db.Components.AnyAsync(c => c.VehicleId == vehicleId && c.Id == componentId, ct))
            throw new NotFoundException();
        if (
            subcomponentId.HasValue
            && !await db.Subcomponents.AnyAsync(
                s =>
                    s.Id == subcomponentId
                    && s.VehicleId == vehicleId
                    && s.VehicleComponentId == componentId,
                ct
            )
        )
            throw new NotFoundException();
        var q = RecordQuery(vehicleId)
            .AsNoTracking()
            .Where(r => r.Components.Any(c => c.VehicleComponentId == componentId));
        if (subcomponentId.HasValue)
            q = q.Where(r => r.Subcomponents.Any(s => s.VehicleSubcomponentId == subcomponentId));
        var rows = await q.OrderByDescending(r => r.Date)
            .ThenByDescending(r => r.CreatedAt)
            .ThenBy(r => r.Id)
            .ToArrayAsync(ct);
        var schedules = db
            .Schedules.AsNoTracking()
            .Include(s => s.Component)
            .Include(s => s.Subcomponent)
            .Where(s =>
                s.VehicleId == vehicleId && s.VehicleComponentId == componentId && !s.Completed
            );
        if (subcomponentId.HasValue)
            schedules = schedules.Where(s =>
                s.VehicleSubcomponentId == null || s.VehicleSubcomponentId == subcomponentId
            );
        var planned = await schedules.ToArrayAsync(ct);
        var next = planned
            .OrderByDescending(s => s.State(Today, v.Mileage))
            .ThenBy(s => s.DueDate ?? DateOnly.MaxValue)
            .ThenBy(s => s.DueMileage ?? int.MaxValue)
            .ThenByDescending(s => s.Priority)
            .FirstOrDefault();
        return new(
            rows.Length,
            rows.Sum(r => r.TotalCost),
            rows.FirstOrDefault()?.ToDto(),
            rows.Take(5).Select(r => r.ToDto()).ToArray(),
            next is null ? null : ScheduleDto(next, v.Mileage),
            rows.Sum(r => r.PendingPaymentCount)
        );
    }

    public async Task<IndicatorsDto> Indicators(Guid vehicleId, int? year, CancellationToken ct)
    {
        await Vehicle(vehicleId, ct);
        var all = await RecordQuery(vehicleId).AsNoTracking().ToListAsync(ct);
        return FinancialSummary.Calculate(all, year ?? Today.Year);
    }

    public static void Pagination(ref int page, ref int size)
    {
        if (page < 1 || page > 1_000_000 || size < 1 || size > 100)
            throw new DomainException("Página deve ser positiva e tamanho entre 1 e 100.");
    }
}

public static class FinancialSummary
{
    public static IndicatorsDto Calculate(IReadOnlyList<MaintenanceRecord> all, int year)
    {
        if (year < 1900 || year > 9999)
            throw new DomainException("Ano inválido.");
        var monthly = Enumerable
            .Range(1, 12)
            .Select(m =>
            {
                var rows = all.Where(r => r.Date.Year == year && r.Date.Month == m).ToArray();
                return new MonthlyExpense(
                    $"{year}-{m:00}",
                    rows.Sum(r => r.PartsCost),
                    rows.Where(r => !r.PackageTotal.HasValue).Sum(r => r.LaborCost),
                    rows.Sum(r => r.TotalCost),
                    rows.Sum(r => r.PackageTotal ?? 0),
                    rows.Sum(r => r.InterestCost)
                );
            })
            .ToArray();
        return new(
            all.Sum(r => r.TotalCost),
            all.Sum(r => r.PartsCost),
            all.Where(r => !r.PackageTotal.HasValue).Sum(r => r.LaborCost),
            all.Count,
            all.Count == 0 ? null : all.Max(r => r.Date),
            monthly,
            all.GroupBy(r => r.Date.Year)
                .OrderBy(g => g.Key)
                .Select(g => new AnnualExpense(g.Key, g.Sum(r => r.TotalCost)))
                .ToArray(),
            all.GroupBy(r => r.Category)
                .Select(g => new CategoryExpense(g.Key, g.Sum(r => r.TotalCost)))
                .OrderByDescending(c => c.Total)
                .ToArray(),
            all.OrderByDescending(r => r.TotalCost).Take(5).Select(r => r.ToDto()).ToArray(),
            all.Sum(r => r.PackageTotal ?? 0),
            all.Sum(r => r.InterestCost),
            all.Sum(r => r.PendingPaymentCount)
        );
    }
}
