using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using MinhaGaragem.Application.AI.Prompts;
using MinhaGaragem.Domain;

namespace MinhaGaragem.Application.AI;

public static partial class MaintenanceDraftValidator
{
    public static MaintenanceAiDraft Parse(
        string json,
        string message,
        Vehicle vehicle,
        IReadOnlyList<VehicleComponent> catalogue,
        DateOnly today
    )
    {
        try
        {
            if (json.Length > 24000)
                throw Invalid();
            var data = JsonSerializer.Deserialize<MaintenanceExtraction>(
                json,
                MaintenanceAiJson.Options
            ) ?? throw Invalid();
            return Validate(data, message, vehicle, catalogue, today);
        }
        catch (JsonException)
        {
            throw Invalid();
        }
        catch (DomainException)
        {
            throw Invalid();
        }
    }

    private static MaintenanceAiDraft Validate(
        MaintenanceExtraction d,
        string message,
        Vehicle vehicle,
        IReadOnlyList<VehicleComponent> catalogue,
        DateOnly today
    )
    {
        if (
            d.Components is null
            || d.Items is null
            || d.Warnings is null
            || d.UnmatchedComponents is null
            || d.Components.Count > 10
            || d.Items.Count > 30
            || d.Warnings.Count > 10
            || d.UnmatchedComponents.Count > 20
        )
            throw Invalid();
        if (d.Mileage is int mileage && !SupportsNumber(message, d.MileageEvidence, mileage))
            throw Invalid();
        if (d.TotalCost is decimal cost && !SupportsNumber(message, d.TotalCostEvidence, cost))
            throw Invalid();
        if (d.Notes is not null && !Evidence(message, d.Notes))
            throw Invalid();
        if (d.Workshop is not null && !Evidence(message, d.Workshop))
            throw Invalid();
        if (d.ServiceDate is DateOnly date && !SupportsDate(message, d.ServiceDateEvidence, date))
            throw Invalid();
        var mapped = new List<AiDraftComponent>();
        foreach (var component in d.Components)
        {
            if (
                component is null
                || !Evidence(message, component.Evidence)
                || component.SubcomponentKeys is null
                || component.SubcomponentKeys.Count > 12
                || component.SubcomponentKeys.Distinct().Count() != component.SubcomponentKeys.Count
            )
                throw Invalid();
            var known = catalogue.SingleOrDefault(c => c.Key == component.Key) ?? throw Invalid();
            if (mapped.Any(c => c.Id == known.Id))
                throw Invalid();
            var subs = component
                .SubcomponentKeys.Select(key =>
                    known.Subcomponents.SingleOrDefault(s => s.Key == key) ?? throw Invalid()
                )
                .Select(s => new SubcomponentDto(s.Id, known.Id, s.Key, s.Name))
                .ToArray();
            mapped.Add(new(known.Id, known.Key, known.Name, subs));
        }
        var items = new List<AiDraftItem>();
        foreach (var item in d.Items)
        {
            if (item is null || !Evidence(message, item.Evidence) || !Evidence(message, item.Name))
                throw Invalid();
            if (
                item.Quantity is int quantity
                && !SupportsNumber(message, item.QuantityEvidence, quantity)
            )
                throw Invalid();
            items.Add(new(item.Name, item.Quantity));
        }
        if (
            d.Warnings.Any(w => string.IsNullOrWhiteSpace(w) || w.Length > 400)
            || d.UnmatchedComponents.Any(w => !Evidence(message, w) || w.Length > 160)
        )
            throw Invalid();
        // Reuse existing monetary/date/odometer business rules without filling absent draft fields.
        Rules.Record(
            new(
                d.ServiceDate ?? today,
                d.Category ?? MaintenanceCategory.General,
                d.ServiceType ?? ServiceType.Inspection,
                d.Description,
                d.Mileage,
                0,
                d.Workshop,
                d.Notes,
                mapped.Select(c => c.Id).ToList(),
                items.Select(i => new ItemInput(i.Name, i.Quantity ?? 1, 0)).ToList(),
                PackageTotal: d.TotalCost ?? 0
            ),
            vehicle.Mileage,
            today
        );
        var missing = new List<string>();
        if (d.ServiceDate is null)
            missing.Add("serviceDate");
        if (d.Mileage is null)
            missing.Add("mileage");
        if (d.TotalCost is null)
            missing.Add("totalCost");
        if (d.Category is null)
            missing.Add("category");
        if (d.ServiceType is null)
            missing.Add("serviceType");
        if (d.Workshop is null)
            missing.Add("workshop");
        if (mapped.Count == 0)
            missing.Add("components");
        if (items.Any(i => i.Quantity is null))
            missing.Add("itemQuantities");
        var warnings = d.Warnings.ToList();
        warnings.AddRange(
            d.UnmatchedComponents.Select(c => "Confirme o conjunto correspondente a: " + c)
        );
        return new(
            vehicle.Id,
            d.Description,
            d.Category,
            d.ServiceType,
            d.Mileage,
            d.TotalCost,
            d.ServiceDate,
            d.Workshop,
            d.Notes,
            mapped,
            items,
            missing,
            warnings,
            MaintenancePromptV1.Version
        );
    }

    private static bool Evidence(string message, string? evidence) =>
        !string.IsNullOrWhiteSpace(evidence)
        && evidence.Length <= message.Length
        && message.Contains(evidence, StringComparison.OrdinalIgnoreCase);

    private static bool SupportsNumber(string message, string? evidence, decimal expected)
    {
        if (!Evidence(message, evidence))
            return false;
        foreach (Match match in Numbers().Matches(evidence!))
        {
            var text = match.Groups["n"].Value;
            if (text.Contains(','))
                text = text.Replace(".", "").Replace(',', '.');
            else if (
                text.Count(c => c == '.') > 1
                || (text.Contains('.') && text.Length - text.LastIndexOf('.') - 1 == 3)
            )
                text = text.Replace(".", "");
            if (
                decimal.TryParse(
                    text,
                    NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingSign,
                    CultureInfo.InvariantCulture,
                    out var value
                )
            )
            {
                if (match.Groups["scale"].Success)
                    value *= 1000;
                if (value == expected)
                    return true;
            }
        }
        return false;
    }

    private static bool SupportsDate(string message, string? evidence, DateOnly date)
    {
        if (!Evidence(message, evidence))
            return false;
        return DateOnly.TryParseExact(
                evidence!.Trim(),
                [
                    "yyyy-MM-dd",
                    "dd/MM/yyyy",
                    "d/M/yyyy",
                    "dd-MM-yyyy",
                    "d-M-yyyy",
                    "d 'de' MMMM 'de' yyyy",
                    "d MMMM yyyy",
                ],
                CultureInfo.GetCultureInfo("pt-BR"),
                DateTimeStyles.None,
                out var parsed
            )
            && parsed == date;
    }

    private static MaintenanceAiException Invalid() => new(AiFailure.InvalidResponse);

    [GeneratedRegex(
        @"(?<![\p{L}\d])(?<n>-?\d+(?:[.,]\d+)*)(?:\s*(?<scale>mil|k)\b)?",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant
    )]
    private static partial Regex Numbers();
}
