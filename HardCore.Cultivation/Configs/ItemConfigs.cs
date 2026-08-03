using Vecxy.Assets;

namespace HardCore.Cultivation;

public sealed class ItemsConfig : IYamlConfig
{
    public List<ItemInfo> Items { get; set; } = [];
}

public sealed class ItemInfo
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Icon { get; set; } = string.Empty;
    public EItemType Type { get; set; }
    public EItemRarity Rarity { get; set; }
    public int MaxStack { get; set; } = 999;
    public bool Consumable { get; set; }
    public List<StatModifier> Effects { get; set; } = [];
}

public sealed class StatModifier
{
    public EStatType Stat { get; set; }
    public double Value { get; set; }
}

public sealed class ItemStack
{
    public string ItemId { get; set; } = string.Empty;
    public int Amount { get; set; } = 1;
}
