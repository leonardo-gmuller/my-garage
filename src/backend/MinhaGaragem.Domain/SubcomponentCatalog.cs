namespace MinhaGaragem.Domain;

/// <summary>Technical choices, not fabricated maintenance records or replacement intervals.</summary>
public static class SubcomponentCatalog
{
    public static readonly (string ComponentKey, string Key, string Name)[] All =
    [
        ("suspension", "shock-absorbers", "Amortecedores"),
        ("suspension", "springs", "Molas"),
        ("suspension", "bump-stops", "Batentes"),
        ("suspension", "control-arms", "Bandejas"),
        ("suspension", "ball-joints", "Pivôs"),
        ("suspension", "bushings", "Buchas"),
        ("engine", "spark-plugs", "Velas"),
        ("engine", "ignition-cables", "Cabos de vela"),
        ("engine", "battery", "Bateria"),
        ("engine", "filters", "Filtros"),
        ("engine", "belts", "Correias"),
        ("engine", "cooling-system", "Sistema de arrefecimento"),
        ("battery", "terminals", "Terminais"),
        ("battery", "battery-unit", "Bateria completa"),
        ("wheels", "tires", "Pneus"),
        ("wheels", "rims", "Rodas"),
        ("brakes", "pads", "Pastilhas"),
        ("brakes", "discs", "Discos"),
        ("brakes", "fluid", "Fluido de freio"),
        ("lights", "bulbs", "Lâmpadas"),
        ("lights", "headlights", "Conjunto dos faróis"),
        ("cooling", "radiator", "Radiador"),
        ("cooling", "hoses", "Mangueiras"),
        ("transmission", "clutch", "Embreagem"),
        ("transmission", "gearbox", "Câmbio"),
        ("exhaust", "muffler", "Silencioso"),
        ("exhaust", "catalyst", "Catalisador"),
        ("body", "doors", "Portas"),
        ("body", "bonnet", "Capô"),
        ("body", "panels", "Painéis externos"),
    ];

    public static VehicleComponent CreateComponent(Guid vehicleId, string key, string name)
    {
        var component = new VehicleComponent
        {
            VehicleId = vehicleId,
            Key = key,
            Name = name,
        };
        component.Subcomponents = All.Where(x => x.ComponentKey == key)
            .Select(x => new VehicleSubcomponent
            {
                VehicleId = vehicleId,
                VehicleComponentId = component.Id,
                Key = x.Key,
                Name = x.Name,
            })
            .ToList();
        return component;
    }
}
