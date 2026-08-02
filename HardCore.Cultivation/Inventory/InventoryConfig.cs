using Vecxy.Assets;

namespace HardCore.Cultivation.Inventory;

public sealed class InventoryConfig : IYamlConfig
{
    public int Capacity { get; set; } = 100;
    public List<InventoryEntryConfig> Stacks { get; set; } = [];

    public void Validate()
    {
        if (Capacity <= 0)
            throw new InvalidDataException("Inventory capacity must be positive.");
        if (Stacks.Sum(stack => stack.Quantity) > Capacity)
            throw new InvalidDataException("Inventory contains more items than its capacity.");
        foreach (var stack in Stacks)
        {
            if (string.IsNullOrWhiteSpace(stack.ItemId))
                throw new InvalidDataException("Inventory stack has no item id.");
            if (stack.Quantity <= 0)
                throw new InvalidDataException($"Inventory stack '{stack.ItemId}' has invalid quantity.");
        }
    }
}

public sealed class InventoryEntryConfig
{
    public string ItemId { get; set; } = string.Empty;
    public int Quantity { get; set; } = 1;
}
