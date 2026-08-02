using Vecxy.Assets;

namespace HardCore.Cultivation.Inventory;

public sealed class ItemCatalogConfig : IYamlConfig
{
    public List<ItemDefinition> Items { get; set; } = [];

    public void Validate()
    {
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var item in Items)
        {
            item.Validate();
            if (!ids.Add(item.Id))
                throw new InvalidDataException($"Duplicate item id: {item.Id}");
        }
    }
}

public sealed class ItemDefinition
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Icon { get; set; } = string.Empty;
    public string Quality { get; set; } = "common";
    public int SellPrice { get; set; }
    public int MaxStack { get; set; } = 99;

    internal void Validate()
    {
        if (string.IsNullOrWhiteSpace(Id))
            throw new InvalidDataException("Item id is required.");
        if (string.IsNullOrWhiteSpace(Name))
            throw new InvalidDataException($"Item '{Id}' has no name.");
        if (string.IsNullOrWhiteSpace(Icon))
            throw new InvalidDataException($"Item '{Id}' has no icon.");
        if (SellPrice < 0)
            throw new InvalidDataException($"Item '{Id}' has a negative sell price.");
        if (MaxStack <= 0)
            throw new InvalidDataException($"Item '{Id}' has an invalid max stack.");
    }
}
