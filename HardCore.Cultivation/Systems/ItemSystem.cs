namespace HardCore.Cultivation;

public sealed class ItemSystem
(
    GameDatabase database,
    Inventory inventory,
    CharacterStats stats
)
{
    public event Action<ItemInfo>? ItemUsed;

    public bool CanUse(string itemId)
    {
        var item = database.GetItem(itemId);
        return item.Consumable && inventory.Has(itemId);
    }

    public bool TryUse(string itemId)
    {
        var item = database.GetItem(itemId);

        if (!item.Consumable || !inventory.TryRemove(itemId))
            return false;

        foreach (var effect in item.Effects)
            stats.Add(effect.Stat, effect.Value);

        ItemUsed?.Invoke(item);
        return true;
    }
}
