using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using MinhaGaragem.Domain;

namespace MinhaGaragem.Application.AI;

/// <summary>Bounded, read-only queries. Financial calculations reuse the maintenance domain.</summary>
public sealed class HistoryQueries(GarageService garage, IGarageDb db)
{
    public static HistoryAnswer Clarify(Guid vehicleId) =>
        new(
            vehicleId,
            "clarification",
            "Posso consultar o último serviço, os gastos e os agendamentos em aberto deste veículo. Informe um componente por pergunta e, se desejar, um ano completo (por exemplo, 2026).",
            "Veículo selecionado",
            0,
            null,
            0,
            [],
            [],
            []
        );

    public async Task<HistoryAnswer> Run(
        Guid vehicleId,
        string kind,
        HistoryFilters filters,
        string question,
        CancellationToken ct
    )
    {
        var vehicle = await garage.Vehicle(vehicleId, ct);
        var year = RequestedYear(question, garage.Today.Year);
        if (year == -1)
            return Clarify(vehicleId);
        if (filters.Year != year)
            throw Invalid();
        VehicleComponent? component = null;
        VehicleSubcomponent? sub = null;
        if (filters.ComponentKey is not null)
            component = await db
                .Components.AsNoTracking()
                .SingleOrDefaultAsync(
                    c => c.VehicleId == vehicleId && c.Key == filters.ComponentKey,
                    ct
                )
                ?? throw Invalid();
        if (filters.SubcomponentKey is not null)
        {
            if (component is null)
                throw Invalid();
            sub = await db
                .Subcomponents.AsNoTracking()
                .SingleOrDefaultAsync(
                    s =>
                        s.VehicleId == vehicleId
                        && s.VehicleComponentId == component.Id
                        && s.Key == filters.SubcomponentKey,
                    ct
                )
                ?? throw Invalid();
        }
        if (
            filters.SearchTerm is not null
            && (
                string.IsNullOrWhiteSpace(filters.SearchTerm)
                || filters.SearchTerm.Length is < 3 or > 60
                || !question.Contains(filters.SearchTerm, StringComparison.OrdinalIgnoreCase)
            )
        )
            throw Invalid();
        var scope =
            (
                sub is not null
                    ? $"{component!.Name} · {sub.Name}"
                    : component?.Name ?? "Todos os componentes"
            ) + (year.HasValue ? $" · {year}" : " · Todo o período registrado");
        if (filters.SearchTerm is not null)
            scope += $" · Descrição contém “{filters.SearchTerm}”";

        if (kind == "pending")
        {
            var query = db
                .Schedules.AsNoTracking()
                .Include(s => s.Component)
                .Include(s => s.Subcomponent)
                .Where(s => s.VehicleId == vehicleId && !s.Completed);
            if (component is not null)
                query = query.Where(s => s.VehicleComponentId == component.Id);
            if (sub is not null)
                query = query.Where(s => s.VehicleSubcomponentId == sub.Id);
            // A schedule without a date cannot be attributed to a calendar year.
            if (year.HasValue)
                query = query.Where(s => s.DueDate.HasValue && s.DueDate.Value.Year == year.Value);
            if (filters.SearchTerm is not null)
            {
                var term = filters.SearchTerm.ToLowerInvariant();
                query = query.Where(s => s.Description.ToLower().Contains(term));
            }
            var planned = await query.Take(2001).ToArrayAsync(ct);
            if (planned.Length > 2000)
                return Narrow(vehicleId, scope);
            var schedules = planned
                .OrderByDescending(s => s.State(garage.Today, vehicle.Mileage))
                .ThenBy(s => s.DueDate ?? DateOnly.MaxValue)
                .ThenBy(s => s.DueMileage ?? int.MaxValue)
                .ThenByDescending(s => s.Priority)
                .ThenBy(s => s.Id)
                .Take(5)
                .Select(s => new AssistantSchedule(
                    garage.ScheduleDto(s, vehicle.Mileage),
                    $"/agenda?vehicle={vehicleId}"
                ))
                .ToArray();
            return new(
                vehicleId,
                kind,
                planned.Length == 0
                    ? "Nenhum agendamento em aberto encontrado neste recorte."
                    : "Estes são os agendamentos ainda não concluídos.",
                scope,
                planned.Length,
                null,
                0,
                [],
                schedules,
                [
                    "A consulta considera apenas agendamentos cadastrados; não é um diagnóstico nem uma recomendação de intervalo.",
                ]
            );
        }
        var records = db
            .Records.AsNoTracking()
            .Where(r => r.VehicleId == vehicleId)
            .Include(r => r.Items)
            .Include(r => r.Payments)
            .AsQueryable();
        if (component is not null)
            records = records.Where(r =>
                r.Components.Any(c => c.VehicleComponentId == component.Id)
            );
        if (sub is not null)
            records = records.Where(r =>
                r.Subcomponents.Any(s => s.VehicleSubcomponentId == sub.Id)
            );
        if (year.HasValue)
            records = records.Where(r => r.Date.Year == year.Value);
        if (filters.SearchTerm is not null)
        {
            var term = filters.SearchTerm.ToLowerInvariant();
            records = records.Where(r => r.Description.ToLower().Contains(term));
        }
        var ordered = records
            .OrderByDescending(r => r.Date)
            .ThenByDescending(r => r.CreatedAt)
            .ThenBy(r => r.Id);
        var rows = await ordered.Take(kind == "latest" ? 1 : 2001).ToArrayAsync(ct);
        if (rows.Length > 2000)
            return Narrow(vehicleId, scope);
        var summary = FinancialSummary.Calculate(rows, year ?? garage.Today.Year);
        List<string> warnings = [];
        if (kind == "spending" && component is not null)
            warnings.Add(
                "O total considera os serviços vinculados ao componente, inclusive serviços com várias peças. Não representa o custo individual da peça e não deve ser somado a outros componentes."
            );
        if (summary.PendingPaymentCount > 0)
            warnings.Add(
                "Há cobranças com valor final ainda não informado. Os valores exibidos são os totais conhecidos e podem aumentar."
            );
        return new(
            vehicleId,
            kind,
            rows.Length == 0
                    ? "Nenhum serviço encontrado neste recorte. Isso não significa que o serviço nunca foi realizado."
                : kind == "latest" ? "Último serviço registrado neste recorte."
                : "Total conhecido dos serviços encontrados, com juros informados e sem duplicar vínculos de componentes.",
            scope,
            rows.Length,
            kind == "spending" ? summary.Total : null,
            summary.PendingPaymentCount,
            rows.Take(5)
                .Select(r => new AssistantRecord(
                    r.Id,
                    r.Description,
                    r.Date,
                    r.Mileage,
                    r.TotalCost,
                    r.PackageTotal.HasValue,
                    r.PendingPaymentCount,
                    $"/historico?vehicle={vehicleId}&record={r.Id}"
                ))
                .ToArray(),
            [],
            warnings
        );
    }

    private static HistoryAnswer Narrow(Guid vehicle, string scope) =>
        new(
            vehicle,
            "clarification",
            "Este recorte excede 2.000 registros. Informe um ano ou um componente para consultar o conjunto completo sem apresentar um total parcial.",
            scope,
            0,
            null,
            0,
            [],
            [],
            []
        );

    internal static int? RequestedYear(string question, int todayYear)
    {
        var text = question.ToLowerInvariant();
        if (
            Regex.IsMatch(
                text,
                @"\b(mês|mes|meses|dia|dias|anos|semana|semanas|hoje|ontem|desde|entre|janeiro|fevereiro|março|marco|abril|maio|junho|julho|agosto|setembro|outubro|novembro|dezembro)\b|\búltimo ano\b|\bultimo ano\b|\búltimos?\s+\d|\bultimos?\s+\d|\d[/-]\d",
                RegexOptions.CultureInvariant,
                TimeSpan.FromMilliseconds(100)
            )
        )
            return -1;
        var years = Regex
            .Matches(
                text,
                @"\b(?:19|20|21)\d{2}\b",
                RegexOptions.CultureInvariant,
                TimeSpan.FromMilliseconds(100)
            )
            .Select(m => int.Parse(m.Value))
            .ToHashSet();
        if (Regex.IsMatch(text, @"\b(este|nesse|neste) ano\b|\bano atual\b"))
            years.Add(todayYear);
        if (Regex.IsMatch(text, @"\bano (passado|anterior)\b"))
            years.Add(todayYear - 1);
        return years.Count > 1 ? -1
            : years.Count == 1 ? years.Single()
            : null;
    }

    private static MaintenanceAiException Invalid() => new(AiFailure.InvalidResponse);
}
