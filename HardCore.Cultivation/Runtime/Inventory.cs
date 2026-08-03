namespace HardCore.Cultivation;

public sealed class Inventory
{
    private readonly Dictionary<string, int> _items = new(StringComparer.Ordinal);

    public event Action<string, int>? Changed;

    public IReadOnlyDictionary<string, int> Items => _items;

    public int GetAmount(string itemId)
    {
        return _items.GetValueOrDefault(itemId);
    }

    public bool Has(string itemId, int amount = 1)
    {
        return amount >= 0 && GetAmount(itemId) >= amount;
    }

    public void Add(string itemId, int amount = 1)
    {
        if (string.IsNullOrWhiteSpace(itemId))
            throw new ArgumentException("Item id is required.", nameof(itemId));

        if (amount <= 0)
            throw new ArgumentOutOfRangeException(nameof(amount));

        _items[itemId] = GetAmount(itemId) + amount;
        Changed?.Invoke(itemId, _items[itemId]);
    }

    public bool TryRemove(string itemId, int amount = 1)
    {
        if (amount <= 0)
            throw new ArgumentOutOfRangeException(nameof(amount));

        var current = GetAmount(itemId);
        if (current < amount)
            return false;

        var next = current - amount;

        if (next == 0)
            _items.Remove(itemId);
        else
            _items[itemId] = next;

        Changed?.Invoke(itemId, next);
        return true;
    }

    public bool CanRemove(IEnumerable<ItemStack> stacks)
    {
        return stacks.All(x => Has(x.ItemId, x.Amount));
    }

    public bool TryRemove(IEnumerable<ItemStack> stacks)
    {
        var list = stacks.ToArray();

        if (!CanRemove(list))
            return false;

        foreach (var stack in list)
            TryRemove(stack.ItemId, stack.Amount);

        return true;
    }

    public void Add(IEnumerable<ItemStack> stacks)
    {
        foreach (var stack in stacks)
            Add(stack.ItemId, stack.Amount);
    }

    public void ReplaceWith(IReadOnlyDictionary<string, int> items)
    {
        _items.Clear();

        foreach (var pair in items)
        {
            if (pair.Value > 0)
                _items[pair.Key] = pair.Value;
        }
    }
}
