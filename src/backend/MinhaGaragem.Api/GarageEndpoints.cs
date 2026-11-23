using Microsoft.EntityFrameworkCore;
using MinhaGaragem.Application;
using MinhaGaragem.Domain;

namespace MinhaGaragem.Api;

public static class GarageEndpoints
{
    public static void MapGarageEndpoints(this WebApplication app)
    {
        var vehicles = app.MapGroup("/api/vehicles").RequireAuthorization().WithTags("Vehicles");
        vehicles.MapGet(
            "/",
            (GarageService s, CancellationToken ct, int page = 1, int pageSize = 20) =>
                s.Vehicles(page, pageSize, ct)
        );
        vehicles.MapPost("/", async (VehicleInput input, GarageService s, CancellationToken ct) =>
        {
            var v = await s.SaveVehicle(null, input, ct);
            return Results.Created($"/api/vehicles/{v.Id}", v);
        });
        vehicles.MapGet(
            "/{vehicleId:guid}",
            async (Guid vehicleId, GarageService s, CancellationToken ct) =>
                (await s.Vehicle(vehicleId, ct)).ToDto()
        );
        vehicles.MapPut(
            "/{vehicleId:guid}",
            (Guid vehicleId, VehicleInput input, GarageService s, CancellationToken ct) =>
                s.SaveVehicle(vehicleId, input, ct)
        );
        vehicles.MapPut(
            "/{vehicleId:guid}/paint",
            (Guid vehicleId, PaintInput input, GarageService s, CancellationToken ct) =>
                s.SavePaint(vehicleId, input, ct)
        );
        vehicles.MapGet(
            "/{vehicleId:guid}/components/{componentId:guid}/summary",
            (
                Guid vehicleId,
                Guid componentId,
                GarageService s,
                CancellationToken ct,
                Guid? subcomponent = null
            ) => s.ComponentSummary(vehicleId, componentId, subcomponent, ct)
        );
        vehicles.MapDelete(
            "/{vehicleId:guid}",
            async (
                Guid vehicleId,
                GarageService s,
                IGarageDb db,
                FileStorage storage,
                CancellationToken ct
            ) =>
            {
                var v = await s.Vehicle(vehicleId, ct);
                var keys = await db
                    .Attachments.Where(a => a.Record.VehicleId == vehicleId)
                    .Select(a => a.StorageKey)
                    .ToListAsync(ct);
                if (v.PhotoKey is not null)
                    keys.Add(v.PhotoKey);
                db.Vehicles.Remove(v);
                await db.SaveChangesAsync(ct);
                foreach (var key in keys)
                    storage.Delete(key);
                return Results.NoContent();
            }
        );
        vehicles.MapPost(
            "/{vehicleId:guid}/mileage",
            async (Guid vehicleId, MileageInput input, GarageService s, CancellationToken ct) =>
            {
                await s.UpdateMileage(vehicleId, input.Mileage, ct);
                return Results.NoContent();
            }
        );
        vehicles.MapGet(
            "/{vehicleId:guid}/mileage",
            async (
                Guid vehicleId,
                GarageService s,
                IGarageDb db,
                CancellationToken ct,
                int page = 1,
                int pageSize = 20
            ) =>
            {
                await s.Vehicle(vehicleId, ct);
                GarageService.Pagination(ref page, ref pageSize);
                var q = db.MileageHistory.AsNoTracking().Where(h => h.VehicleId == vehicleId);
                return new Page<object>(
                    await q.OrderByDescending(h => h.RecordedAt)
                        .Skip((page - 1) * pageSize)
                        .Take(pageSize)
                        .Select(h =>
                            (object)
                                new
                                {
                                    h.Id,
                                    h.Mileage,
                                    h.RecordedAt,
                                }
                        )
                        .ToArrayAsync(ct),
                    await q.CountAsync(ct),
                    page,
                    pageSize
                );
            }
        );
        vehicles.MapGet(
            "/{vehicleId:guid}/components",
            async (Guid vehicleId, GarageService s, IGarageDb db, CancellationToken ct) =>
            {
                await s.Vehicle(vehicleId, ct);
                var components = await db
                    .Components.AsNoTracking()
                    .Include(c => c.Subcomponents)
                    .Where(c => c.VehicleId == vehicleId)
                    .OrderBy(c => c.Name)
                    .ToArrayAsync(ct);
                return components
                    .Select(c => new ComponentDto(
                        c.Id,
                        c.Key,
                        c.Name,
                        c.Subcomponents.OrderBy(s => s.Name).Select(s => s.ToDto()).ToArray()
                    ))
                    .ToArray();
            }
        );
        vehicles.MapGet(
            "/{vehicleId:guid}/indicators",
            (Guid vehicleId, GarageService s, CancellationToken ct, int? year) =>
                s.Indicators(vehicleId, year, ct)
        );
        vehicles.MapGet(
            "/{vehicleId:guid}/photo",
            async (Guid vehicleId, GarageService s, FileStorage storage, CancellationToken ct) =>
            {
                var v = await s.Vehicle(vehicleId, ct);
                if (v.PhotoKey is null)
                    throw new NotFoundException();
                return Results.File(
                    storage.Open(v.PhotoKey),
                    v.PhotoContentType ?? "application/octet-stream"
                );
            }
        );
        vehicles
            .MapPost(
                "/{vehicleId:guid}/photo",
                async (
                    Guid vehicleId,
                    IFormFile file,
                    GarageService s,
                    IGarageDb db,
                    FileStorage storage,
                    CancellationToken ct
                ) =>
                {
                    var v = await s.Vehicle(vehicleId, ct);
                    var saved = await storage.Save(file, true, ct);
                    var old = v.PhotoKey;
                    v.PhotoKey = saved.Key;
                    v.PhotoContentType = saved.ContentType;
                    try
                    {
                        await db.SaveChangesAsync(ct);
                    }
                    catch
                    {
                        storage.Delete(saved.Key);
                        throw;
                    }
                    if (old is not null)
                        storage.Delete(old);
                    return Results.Ok(v.ToDto());
                }
            )
            .DisableAntiforgery();
        var records = vehicles
            .MapGroup("/{vehicleId:guid}/records")
            .WithTags("Maintenance records");
        records.MapGet(
            "/",
            (
                Guid vehicleId,
                GarageService s,
                CancellationToken ct,
                int page = 1,
                int pageSize = 20,
                DateOnly? from = null,
                DateOnly? to = null,
                MaintenanceCategory? category = null,
                Guid? component = null,
                Guid? subcomponent = null
            ) =>
                s.Records(
                    vehicleId,
                    page,
                    pageSize,
                    from,
                    to,
                    category,
                    component,
                    ct,
                    subcomponent
                )
        );
        records.MapGet(
            "/{recordId:guid}",
            async (Guid vehicleId, Guid recordId, GarageService s, CancellationToken ct) =>
                (await s.Record(vehicleId, recordId, ct)).ToDto()
        );
        records.MapPost(
            "/",
            async (Guid vehicleId, RecordInput input, GarageService s, CancellationToken ct) =>
            {
                var r = await s.SaveRecord(vehicleId, null, input, ct);
                return Results.Created($"/api/vehicles/{vehicleId}/records/{r.Id}", r);
            }
        );
        records.MapPut(
            "/{recordId:guid}",
            (
                Guid vehicleId,
                Guid recordId,
                RecordInput input,
                GarageService s,
                CancellationToken ct
            ) => s.SaveRecord(vehicleId, recordId, input, ct)
        );
        records.MapDelete(
            "/{recordId:guid}",
            async (
                Guid vehicleId,
                Guid recordId,
                GarageService s,
                IGarageDb db,
                FileStorage storage,
                CancellationToken ct
            ) =>
            {
                var r = await s.Record(vehicleId, recordId, ct);
                var keys = r.Attachments.Select(a => a.StorageKey).ToArray();
                db.Records.Remove(r);
                await db.SaveChangesAsync(ct);
                foreach (var key in keys)
                    storage.Delete(key);
                return Results.NoContent();
            }
        );
        records.MapPost(
            "/{recordId:guid}/items",
            async (
                Guid vehicleId,
                Guid recordId,
                ItemInput input,
                GarageService s,
                IGarageDb db,
                CancellationToken ct
            ) =>
            {
                var r = await s.Record(vehicleId, recordId, ct);
                Rules.Item(input);
                if (r.Items.Count >= 100)
                    throw new DomainException("Limite de 100 peças por serviço.");
                var i = new MaintenanceItem
                {
                    MaintenanceRecordId = r.Id,
                    Name = input.Name.Trim(),
                    Quantity = input.Quantity,
                    UnitCost = input.UnitCost,
                };
                db.Items.Add(i);
                await db.SaveChangesAsync(ct);
                return Results.Ok(new ItemDto(i.Id, i.Name, i.Quantity, i.UnitCost, i.Total));
            }
        );
        records.MapPut(
            "/{recordId:guid}/items/{itemId:guid}",
            async (
                Guid vehicleId,
                Guid recordId,
                Guid itemId,
                ItemInput input,
                GarageService s,
                IGarageDb db,
                CancellationToken ct
            ) =>
            {
                var r = await s.Record(vehicleId, recordId, ct);
                var i = r.Items.SingleOrDefault(i => i.Id == itemId)
                    ?? throw new NotFoundException();
                Rules.Item(input);
                i.Name = input.Name.Trim();
                i.Quantity = input.Quantity;
                i.UnitCost = input.UnitCost;
                await db.SaveChangesAsync(ct);
                return Results.NoContent();
            }
        );
        records.MapDelete(
            "/{recordId:guid}/items/{itemId:guid}",
            async (
                Guid vehicleId,
                Guid recordId,
                Guid itemId,
                GarageService s,
                IGarageDb db,
                CancellationToken ct
            ) =>
            {
                var r = await s.Record(vehicleId, recordId, ct);
                var i = r.Items.SingleOrDefault(i => i.Id == itemId)
                    ?? throw new NotFoundException();
                db.Items.Remove(i);
                await db.SaveChangesAsync(ct);
                return Results.NoContent();
            }
        );
        records
            .MapPost(
                "/{recordId:guid}/attachments",
                async (
                    Guid vehicleId,
                    Guid recordId,
                    IFormFile file,
                    GarageService s,
                    IGarageDb db,
                    FileStorage storage,
                    CancellationToken ct
                ) =>
                {
                    var r = await s.Record(vehicleId, recordId, ct);
                    if (r.Attachments.Count >= 20)
                        throw new DomainException("Limite de 20 anexos por serviço.");
                    var saved = await storage.Save(file, false, ct);
                    var a = new MaintenanceAttachment
                    {
                        MaintenanceRecordId = r.Id,
                        FileName = Path.GetFileName(file.FileName),
                        StorageKey = saved.Key,
                        ContentType = saved.ContentType,
                        Size = file.Length,
                    };
                    db.Attachments.Add(a);
                    try
                    {
                        await db.SaveChangesAsync(ct);
                    }
                    catch
                    {
                        storage.Delete(saved.Key);
                        throw;
                    }
                    return Results.Created(
                        $"/api/vehicles/{vehicleId}/records/{recordId}/attachments/{a.Id}",
                        new AttachmentDto(
                            a.Id,
                            a.FileName,
                            a.ContentType,
                            a.Size,
                            $"/api/vehicles/{vehicleId}/records/{recordId}/attachments/{a.Id}"
                        )
                    );
                }
            )
            .DisableAntiforgery();
        records.MapGet(
            "/{recordId:guid}/attachments/{attachmentId:guid}",
            async (
                Guid vehicleId,
                Guid recordId,
                Guid attachmentId,
                GarageService s,
                FileStorage storage,
                CancellationToken ct
            ) =>
            {
                var r = await s.Record(vehicleId, recordId, ct);
                var a = r.Attachments.SingleOrDefault(a => a.Id == attachmentId)
                    ?? throw new NotFoundException();
                return Results.File(storage.Open(a.StorageKey), a.ContentType, a.FileName);
            }
        );
        records.MapDelete(
            "/{recordId:guid}/attachments/{attachmentId:guid}",
            async (
                Guid vehicleId,
                Guid recordId,
                Guid attachmentId,
                GarageService s,
                IGarageDb db,
                FileStorage storage,
                CancellationToken ct
            ) =>
            {
                var r = await s.Record(vehicleId, recordId, ct);
                var a = r.Attachments.SingleOrDefault(a => a.Id == attachmentId)
                    ?? throw new NotFoundException();
                db.Attachments.Remove(a);
                await db.SaveChangesAsync(ct);
                storage.Delete(a.StorageKey);
                return Results.NoContent();
            }
        );
        vehicles.MapGet(
            "/{vehicleId:guid}/timeline",
            (
                Guid vehicleId,
                GarageService s,
                CancellationToken ct,
                int page = 1,
                int pageSize = 20,
                DateOnly? from = null,
                DateOnly? to = null,
                MaintenanceCategory? category = null,
                Guid? component = null,
                Guid? subcomponent = null
            ) =>
                s.Records(
                    vehicleId,
                    page,
                    pageSize,
                    from,
                    to,
                    category,
                    component,
                    ct,
                    subcomponent
                )
        );
        var schedules = vehicles
            .MapGroup("/{vehicleId:guid}/schedules")
            .WithTags("Maintenance schedules");
        schedules.MapGet(
            "/",
            async (
                Guid vehicleId,
                GarageService s,
                IGarageDb db,
                CancellationToken ct,
                int page = 1,
                int pageSize = 20
            ) =>
            {
                var v = await s.Vehicle(vehicleId, ct);
                GarageService.Pagination(ref page, ref pageSize);
                var q = db.Schedules.AsNoTracking().Where(s => s.VehicleId == vehicleId);
                var total = await q.CountAsync(ct);
                var rows = await q.Include(s => s.Component)
                    .Include(s => s.Subcomponent)
                    .OrderBy(s => s.Completed)
                    .ThenBy(s => s.DueDate)
                    .ThenBy(s => s.DueMileage)
                    .ThenBy(s => s.Id)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .ToArrayAsync(ct);
                return new Page<ScheduleDto>(
                    rows.Select(x => s.ScheduleDto(x, v.Mileage)).ToArray(),
                    total,
                    page,
                    pageSize
                );
            }
        );
        schedules.MapPost(
            "/",
            (Guid vehicleId, ScheduleInput input, GarageService s, CancellationToken ct) =>
                s.SaveSchedule(vehicleId, null, input, ct)
        );
        schedules.MapPut(
            "/{scheduleId:guid}",
            (
                Guid vehicleId,
                Guid scheduleId,
                ScheduleInput input,
                GarageService s,
                CancellationToken ct
            ) => s.SaveSchedule(vehicleId, scheduleId, input, ct)
        );
        schedules.MapDelete(
            "/{scheduleId:guid}",
            async (
                Guid vehicleId,
                Guid scheduleId,
                GarageService s,
                IGarageDb db,
                CancellationToken ct
            ) =>
            {
                await s.Vehicle(vehicleId, ct);
                var item = await db.Schedules.SingleOrDefaultAsync(
                    s => s.Id == scheduleId && s.VehicleId == vehicleId,
                    ct
                ) ?? throw new NotFoundException();
                db.Schedules.Remove(item);
                await db.SaveChangesAsync(ct);
                return Results.NoContent();
            }
        );
    }
}
