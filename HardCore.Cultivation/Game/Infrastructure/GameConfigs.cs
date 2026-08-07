using HardCore.Cultivation.Game.Domain;
using Vecxy.Assets;

namespace HardCore.Cultivation.Game.Infrastructure;

public sealed class GameBalanceConfig : IYamlConfig
{
    public int TicksPerYear { get; init; } = 48;
    public int RealMillisecondsPerTick { get; init; } = 1000;
    public int AutoSaveEveryTicks { get; init; } = 12;
    public decimal BaseSpiritualPowerPerTick { get; init; } = 1m;
    public decimal MinimumTickEfficiency { get; init; } = 0.1m;
    public decimal MinimumAgingMultiplier { get; init; } = 0m;
    public decimal MaximumBreakthroughChance { get; init; } = 100m;
    public decimal EffectQualityBase { get; init; } = 0.5m;
    public decimal EffectQualityPerPoint { get; init; } = 0.2m;
    public long StartingMoney { get; init; } = 1000;
    public decimal StartingAgeYears { get; init; } = 16m;
    public decimal MaximumAgeYears { get; init; } = 80m;
    public int MaximumMissionQueueSize { get; init; } = 6;
    public List<QualityBand> QualityBands { get; init; } = [];
    public List<PriceCurvePoint> QualityPriceCurve { get; init; } = [];
}

public sealed class QualityBand
{
    public int Index { get; init; }
    public decimal Weight { get; init; }
}

public sealed class PriceCurvePoint
{
    public decimal Quality { get; init; }
    public decimal Multiplier { get; init; }
}

public sealed class RaritiesConfig : IYamlConfig
{
    public List<RarityConfig> Rarities { get; init; } = [];
}

public sealed class RarityConfig
{
    public ItemRarity Rarity { get; init; }
    public string DisplayName { get; init; } = string.Empty;
    public decimal PriceMultiplier { get; init; } = 1m;
    public decimal ShopWeight { get; init; } = 1m;
    public string Color { get; init; } = "#ffffff";
}

public sealed class ItemsConfig : IYamlConfig
{
    public List<ItemConfig> Items { get; init; } = [];
}

public sealed class ItemConfig
{
    public string Id { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public string Icon { get; init; } = string.Empty;
    public ItemCategory Category { get; init; }
    public ItemDurationType DurationType { get; init; }
    public long BasePrice { get; init; }
    public int TemporaryDurationTicks { get; init; }
    public decimal ShopWeight { get; init; } = 1m;
    public List<ItemEffectDefinition> Effects { get; init; } = [];
}

public sealed class MissionsConfig : IYamlConfig
{
    public int BoardSlotCount { get; init; } = 6;
    public List<MissionConfig> Missions { get; init; } = [];
}

public sealed class MissionConfig
{
    public string Id { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public int MinimumDurationTicks { get; init; } = 1;
    public int MaximumDurationTicks { get; init; } = 500;
    public decimal BoardWeight { get; init; } = 1m;
    public MissionRewardConfig Reward { get; init; } = new();
}

public sealed class MissionRewardConfig
{
    public ItemCategory? RequiredItemCategory { get; init; }
    public int MinimumQuantity { get; init; } = 1;
    public int MaximumQuantity { get; init; } = 1;
    public long Money { get; init; }
}

public sealed class CultivationConfig : IYamlConfig
{
    public decimal BaseRequiredPower { get; init; } = 100m;
    public List<decimal> LevelMultipliers { get; init; } = [];
    public List<CultivationStageConfig> Stages { get; init; } = [];
}

public sealed class CultivationStageConfig
{
    public string Id { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public decimal StageMultiplier { get; init; } = 1m;
    public decimal BaseBreakthroughChance { get; init; }
}

public sealed class ShopConfig : IYamlConfig
{
    public int SlotCount { get; init; } = 6;
    public int MinimumQuantity { get; init; } = 1;
    public int MaximumQuantity { get; init; } = 5;
    public int MinimumBuyMarkup { get; init; } = 25;
    public int MaximumBuyMarkup { get; init; } = 100;
    public int SellAdjustmentPercent { get; init; } = -33;
}

public sealed class GameDatabase
{
    private readonly Dictionary<string, ItemConfig> _items = new(StringComparer.Ordinal);
    private readonly Dictionary<string, MissionConfig> _missions = new(StringComparer.Ordinal);
    private readonly Dictionary<ItemRarity, RarityConfig> _rarities = [];

    public GameBalanceConfig Balance { get; private set; } = new();
    public CultivationConfig Cultivation { get; private set; } = new();
    public ShopConfig Shop { get; private set; } = new();
    public int MissionBoardSlotCount { get; private set; } = 6;
    public IReadOnlyDictionary<string, ItemConfig> Items => _items;
    public IReadOnlyDictionary<string, MissionConfig> Missions => _missions;
    public IReadOnlyDictionary<ItemRarity, RarityConfig> Rarities => _rarities;

    public void Initialize(
        ConfigRef<GameBalanceConfig> balance,
        ConfigRef<RaritiesConfig> rarities,
        ConfigRef<ItemsConfig> items,
        ConfigRef<MissionsConfig> missions,
        ConfigRef<CultivationConfig> cultivation,
        ConfigRef<ShopConfig> shop)
        => Initialize(balance.Value, rarities.Value, items.Value, missions.Value, cultivation.Value, shop.Value);

    public void Initialize(
        GameBalanceConfig balance,
        RaritiesConfig rarities,
        ItemsConfig items,
        MissionsConfig missions,
        CultivationConfig cultivation,
        ShopConfig shop)
    {
        Balance = balance;
        Cultivation = cultivation;
        Shop = shop;
        MissionBoardSlotCount = missions.BoardSlotCount;
        _items.Clear();
        _missions.Clear();
        _rarities.Clear();

        foreach (var item in items.Items)
            AddUnique(_items, item.Id, item, "item");
        foreach (var mission in missions.Missions)
            AddUnique(_missions, mission.Id, mission, "mission");
        foreach (var rarity in rarities.Rarities)
        {
            if (!_rarities.TryAdd(rarity.Rarity, rarity))
                throw new InvalidDataException($"Duplicate rarity: {rarity.Rarity}");
        }

        Validate();
    }

    public ItemConfig GetItem(string id) => _items.TryGetValue(id, out var item)
        ? item
        : throw new KeyNotFoundException($"Unknown item: {id}");

    public MissionConfig GetMission(string id) => _missions.TryGetValue(id, out var mission)
        ? mission
        : throw new KeyNotFoundException($"Unknown mission: {id}");

    public RarityConfig GetRarity(ItemRarity rarity) => _rarities.TryGetValue(rarity, out var config)
        ? config
        : throw new KeyNotFoundException($"Unknown rarity: {rarity}");

    private void Validate()
    {
        if (Balance.TicksPerYear <= 0 || Balance.RealMillisecondsPerTick <= 0)
            throw new InvalidDataException("Tick settings must be positive.");
        if (Balance.MaximumAgeYears <= Balance.StartingAgeYears || Balance.MaximumMissionQueueSize <= 0)
            throw new InvalidDataException("Lifetime and mission queue settings are invalid.");
        if (Balance.QualityBands.Count == 0 || Balance.QualityBands.Any(band => band.Index is < 1 or > 5 || band.Weight <= 0m))
            throw new InvalidDataException("Quality bands are invalid.");
        if (Balance.QualityPriceCurve.Count < 2)
            throw new InvalidDataException("Quality price curve requires at least two points.");
        if (_rarities.Count != Enum.GetValues<ItemRarity>().Length)
            throw new InvalidDataException("Every item rarity must be configured.");
        if (Cultivation.LevelMultipliers.Count != 10 || Cultivation.Stages.Count == 0)
            throw new InvalidDataException("Cultivation requires ten level multipliers and at least one stage.");
        if (_items.Count == 0 || _missions.Count == 0)
            throw new InvalidDataException("Items and missions cannot be empty.");
        if (MissionBoardSlotCount <= 0 || MissionBoardSlotCount > _missions.Count)
            throw new InvalidDataException("Mission board slot count is invalid.");

        foreach (var item in _items.Values)
        {
            if (item.BasePrice < 0 || item.ShopWeight <= 0m)
                throw new InvalidDataException($"Invalid item balance: {item.Id}");
            if (item.DurationType == ItemDurationType.Temporary && item.TemporaryDurationTicks <= 0)
                throw new InvalidDataException($"Temporary item has no duration: {item.Id}");
        }

        foreach (var mission in _missions.Values)
        {
            if (mission.MinimumDurationTicks <= 0 ||
                mission.MaximumDurationTicks < mission.MinimumDurationTicks ||
                mission.BoardWeight <= 0m)
            {
                throw new InvalidDataException($"Invalid mission balance: {mission.Id}");
            }
        }
    }

    private static void AddUnique<T>(IDictionary<string, T> target, string id, T value, string kind)
    {
        if (string.IsNullOrWhiteSpace(id) || !target.TryAdd(id, value))
            throw new InvalidDataException($"Invalid or duplicate {kind} id: {id}");
    }
}
