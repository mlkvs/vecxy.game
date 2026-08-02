namespace HardCore.Cultivation.Inventory;

public sealed class InventoryState
{
    private readonly List<InventoryStack> _stacks;

    public int Capacity { get; }
    public IReadOnlyList<InventoryStack> Stacks => _stacks;
    public int UsedCapacity => _stacks.Sum(stack => stack.Quantity);
    public int? SelectedIndex { get; private set; }
    public InventoryStack? Selected =>
        SelectedIndex is { } index && index >= 0 && index < _stacks.Count
            ? _stacks[index]
            : null;

    public InventoryState(ItemCatalogConfig catalog, InventoryConfig inventory)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(inventory);
        catalog.Validate();
        inventory.Validate();

        Capacity = inventory.Capacity;
        var definitions = catalog.Items.ToDictionary(item => item.Id, StringComparer.Ordinal);
        _stacks = inventory.Stacks.Select(entry =>
        {
            if (!definitions.TryGetValue(entry.ItemId, out var item))
                throw new InvalidDataException($"Unknown inventory item: {entry.ItemId}");
            if (entry.Quantity > item.MaxStack)
                throw new InvalidDataException($"Stack '{entry.ItemId}' exceeds max stack {item.MaxStack}.");
            return new InventoryStack(item, entry.Quantity);
        }).ToList();
    }

    public bool Select(int index)
    {
        if ((uint)index >= (uint)_stacks.Count)
            return false;
        SelectedIndex = index;
        return true;
    }

    public void ClearSelection() => SelectedIndex = null;

    public int SellSelected()
    {
        if (SelectedIndex is not { } index || (uint)index >= (uint)_stacks.Count)
            return 0;

        var stack = _stacks[index];
        var value = stack.Item.SellPrice;
        stack.Quantity--;
        if (stack.Quantity == 0)
            _stacks.RemoveAt(index);
        SelectedIndex = null;
        return value;
    }

    public void Sort()
    {
        _stacks.Sort((left, right) =>
        {
            var quality = QualityRank(right.Item.Quality).CompareTo(QualityRank(left.Item.Quality));
            return quality != 0
                ? quality
                : string.Compare(left.Item.Name, right.Item.Name, StringComparison.Ordinal);
        });
        SelectedIndex = null;
    }

    private static int QualityRank(string quality) => quality.ToLowerInvariant() switch
    {
        "legendary" => 4,
        "epic" => 3,
        "rare" => 2,
        "uncommon" => 1,
        _ => 0
    };
}

public sealed class InventoryStack(ItemDefinition item, int quantity)
{
    public ItemDefinition Item { get; } = item;
    public int Quantity { get; internal set; } = quantity;
}
