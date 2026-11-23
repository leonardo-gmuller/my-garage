using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using MinhaGaragem.Domain;

namespace MinhaGaragem.Application;

public sealed class MechanicService(IGarageDb db, ICurrentUser user, GarageService garage)
{
    private IQueryable<Mechanic> Owned => db.Mechanics.Where(m => m.UserId == user.Id);
    private static readonly Expression<Func<Mechanic, MechanicDto>> Projection = m =>
        new(
            m.Id,
            m.Name,
            m.Workshop,
            m.Specialty,
            m.Phone,
            m.Email,
            m.Address,
            m.Notes,
            m.IsArchived,
            m.Records.Count,
            m.Records.Max(r => (DateOnly?)r.Date)
        );

    public async Task<Page<MechanicDto>> List(
        int page,
        int size,
        string? search,
        string status,
        CancellationToken ct
    )
    {
        GarageService.Pagination(ref page, ref size);
        Rules.Text(search, "Busca", 200);
        if (status is not ("active" or "archived" or "all"))
            throw new DomainException("Situação do mecânico inválida.");
        var query = Owned.AsNoTracking();
        if (status != "all")
            query = query.Where(m => m.IsArchived == (status == "archived"));
        var term = MechanicRules.Optional(search)?.ToLowerInvariant();
        if (term is not null)
            query = query.Where(m =>
                m.Name.ToLower().Contains(term)
                || (m.Workshop != null && m.Workshop.ToLower().Contains(term))
                || (m.Specialty != null && m.Specialty.ToLower().Contains(term))
                || (m.Phone != null && m.Phone.Contains(term))
            );
        var total = await query.CountAsync(ct);
        var rows = await query
            .OrderBy(m => m.Name)
            .ThenBy(m => m.Id)
            .Skip((page - 1) * size)
            .Take(size)
            .Select(Projection)
            .ToArrayAsync(ct);
        return new(rows, total, page, size);
    }

    public async Task<MechanicDto> Get(Guid id, CancellationToken ct) =>
        await Owned
            .AsNoTracking()
            .Where(m => m.Id == id)
            .Select(Projection)
            .SingleOrDefaultAsync(ct)
        ?? throw new NotFoundException();

    public async Task<MechanicDto> Save(Guid? id, MechanicInput input, CancellationToken ct)
    {
        MechanicRules.Validate(input);
        var mechanic = id.HasValue
            ? await Owned.SingleOrDefaultAsync(m => m.Id == id, ct) ?? throw new NotFoundException()
            : new Mechanic { UserId = user.Id };
        mechanic.Name = input.Name.Trim();
        mechanic.Workshop = MechanicRules.Optional(input.Workshop);
        mechanic.Specialty = MechanicRules.Optional(input.Specialty);
        mechanic.Phone = MechanicRules.Optional(input.Phone);
        mechanic.Email = MechanicRules.Optional(input.Email);
        mechanic.Address = MechanicRules.Optional(input.Address);
        mechanic.Notes = MechanicRules.Optional(input.Notes);
        mechanic.IsArchived = input.IsArchived;
        if (id is null)
            db.Mechanics.Add(mechanic);
        await db.SaveChangesAsync(ct);
        return await Get(mechanic.Id, ct);
    }

    public async Task<MechanicDto> SetArchived(Guid id, bool archived, CancellationToken ct)
    {
        var mechanic = await Owned.SingleOrDefaultAsync(m => m.Id == id, ct)
            ?? throw new NotFoundException();
        mechanic.IsArchived = archived;
        await db.SaveChangesAsync(ct);
        return await Get(id, ct);
    }

    public async Task<Page<RecordDto>> Records(Guid id, int page, int size, CancellationToken ct)
    {
        await Get(id, ct);
        GarageService.Pagination(ref page, ref size);
        var query = garage.RecordQuery().AsNoTracking().Where(r => r.MechanicId == id);
        var total = await query.CountAsync(ct);
        var rows = await query
            .OrderByDescending(r => r.Date)
            .ThenByDescending(r => r.CreatedAt)
            .ThenBy(r => r.Id)
            .Skip((page - 1) * size)
            .Take(size)
            .ToArrayAsync(ct);
        return new(rows.Select(r => r.ToDto()).ToArray(), total, page, size);
    }
}
