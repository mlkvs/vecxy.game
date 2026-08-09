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
    public int? DangerLevel { get; init; }
    public List<string> PossibleMonsterIds { get; init; } = [];
    public List<string> PossibleBackgroundIds { get; init; } = [];
    public MissionRewardConfig Reward { get; init; } = new();
}

public sealed class MonstersConfig : IYamlConfig
{
    public List<MonsterConfig> Monsters { get; init; } = [];
}

public sealed class MonsterConfig
{
    public string Id { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string SpriteSet { get; init; } = string.Empty;
    public decimal MaximumHealth { get; init; } = 100m;
    public decimal Attack { get; init; } = 10m;
    public decimal Defense { get; init; }
    public float AttacksPerSecond { get; init; } = 1f;
    public decimal SelectionWeight { get; init; } = 1m;
}

public sealed class CombatConfig : IYamlConfig
{
    public int RenderWidth { get; init; } = 576;
    public int RenderHeight { get; init; } = 324;
    public string HeroSpriteSet { get; init; } = "Textures/Characters/3 Man/Man";
    public decimal HeroBaseHealth { get; init; } = 120m;
    public decimal HeroHealthPerStage { get; init; } = 40m;
    public decimal HeroHealthPerLevel { get; init; } = 8m;
    public decimal HeroBaseAttack { get; init; } = 18m;
    public decimal HeroAttackPerStage { get; init; } = 6m;
    public decimal HeroAttackPerLevel { get; init; } = 2m;
    public decimal HeroBaseDefense { get; init; } = 3m;
    public decimal HeroDefensePerStage { get; init; } = 2m;
    public decimal HeroDefensePerLevel { get; init; } = 0.5m;
    public float HeroAttacksPerSecond { get; init; } = 1.1f;
    public decimal HealthRegenerationPerSecond { get; init; } = 0.1m;
    public decimal RecoveryHealthFraction { get; init; } = 0.30m;
    public float FinishDelaySeconds { get; init; } = 1.2f;
    public List<CombatDangerConfig> DangerLevels { get; init; } = [];
    public List<CombatBackgroundConfig> Backgrounds { get; init; } = [];
}

public sealed class CombatDangerConfig
{
    public int Level { get; init; }
    public decimal EncounterChancePercent { get; init; }
    public decimal MonsterPowerMultiplier { get; init; } = 1m;
}

public sealed class CombatBackgroundConfig
{
    public string Id { get; init; } = string.Empty;
    public List<string> Layers { get; init; } = [];
}

public sealed class DogConfig : IYamlConfig
{
    public string MeditatingTexture { get; init; } = "Textures/Dog.png";
    public string ChargedTexture { get; init; } = "Textures/Dog2.png";
    public float LocalPositionX { get; init; } = -390f;
    public float LocalPositionY { get; init; } = 50f;
    public float BaseScale { get; init; } = 0.28f;
    public float ChargeDurationSeconds { get; init; } = 60f;
    public int RewardUnitRubles { get; init; } = 1000;
    public int MinimumRewardUnits { get; init; } = 1;
    public int MaximumRewardUnits { get; init; } = 5;
    public float MinimumChargeScale { get; init; } = 0.96f;
    public float MaximumChargeScale { get; init; } = 1.02f;
    public float BobAmplitude { get; init; } = 5f;
    public float BobSpeed { get; init; } = 3.1f;
    public float SwayAmplitudeRadians { get; init; } = 0.022f;
    public float SwaySpeed { get; init; } = 1.8f;
    public float BreathingAmplitude { get; init; } = 0.008f;
    public float BreathingSpeed { get; init; } = 2.2f;
    public float GlowScale { get; init; } = 1.12f;
    public float GlowPulseAmplitude { get; init; } = 0.06f;
    public float GlowPulseSpeed { get; init; } = 2.8f;
    public float GlowRed { get; init; } = 1f;
    public float GlowGreen { get; init; } = 0.72f;
    public float GlowBlue { get; init; } = 0.22f;
    public float GlowAlpha { get; init; } = 0.28f;
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
    private readonly Dictionary<string, MonsterConfig> _monsters = new(StringComparer.Ordinal);
    private readonly Dictionary<string, CombatBackgroundConfig> _backgrounds = new(StringComparer.Ordinal);
    private readonly Dictionary<ItemRarity, RarityConfig> _rarities = [];

    public GameBalanceConfig Balance { get; private set; } = new();
    public CultivationConfig Cultivation { get; private set; } = new();
    public ShopConfig Shop { get; private set; } = new();
    public CombatConfig Combat { get; private set; } = new();
    public DogConfig Dog { get; private set; } = new();
    public int MissionBoardSlotCount { get; private set; } = 6;
    public IReadOnlyDictionary<string, ItemConfig> Items => _items;
    public IReadOnlyDictionary<string, MissionConfig> Missions => _missions;
    public IReadOnlyDictionary<string, MonsterConfig> Monsters => _monsters;
    public IReadOnlyDictionary<ItemRarity, RarityConfig> Rarities => _rarities;

    public void Initialize(
        ConfigRef<GameBalanceConfig> balance,
        ConfigRef<RaritiesConfig> rarities,
        ConfigRef<ItemsConfig> items,
        ConfigRef<MissionsConfig> missions,
        ConfigRef<CultivationConfig> cultivation,
        ConfigRef<ShopConfig> shop,
        ConfigRef<MonstersConfig> monsters,
        ConfigRef<CombatConfig> combat,
        ConfigRef<DogConfig> dog)
        => Initialize(balance.Value, rarities.Value, items.Value, missions.Value, cultivation.Value, shop.Value, monsters.Value, combat.Value, dog.Value);

    public void Initialize(
        GameBalanceConfig balance,
        RaritiesConfig rarities,
        ItemsConfig items,
        MissionsConfig missions,
        CultivationConfig cultivation,
        ShopConfig shop,
        MonstersConfig? monsters = null,
        CombatConfig? combat = null,
        DogConfig? dog = null)
    {
        Balance = balance;
        Cultivation = cultivation;
        Shop = shop;
        Combat = combat ?? CreateDefaultCombat();
        Dog = dog ?? new DogConfig();
        MissionBoardSlotCount = missions.BoardSlotCount;
        _items.Clear();
        _missions.Clear();
        _rarities.Clear();
        _monsters.Clear();
        _backgrounds.Clear();

        foreach (var item in items.Items)
            AddUnique(_items, item.Id, item, "item");
        foreach (var mission in missions.Missions)
            AddUnique(_missions, mission.Id, mission, "mission");
        foreach (var rarity in rarities.Rarities)
        {
            if (!_rarities.TryAdd(rarity.Rarity, rarity))
                throw new InvalidDataException($"Duplicate rarity: {rarity.Rarity}");
        }
        foreach (var monster in (monsters ?? CreateDefaultMonsters()).Monsters)
            AddUnique(_monsters, monster.Id, monster, "monster");
        foreach (var background in Combat.Backgrounds)
            AddUnique(_backgrounds, background.Id, background, "combat background");

        Validate();
    }

    public ItemConfig GetItem(string id) => _items.TryGetValue(id, out var item)
        ? item
        : throw new KeyNotFoundException($"Unknown item: {id}");

    public MissionConfig GetMission(string id) => _missions.TryGetValue(id, out var mission)
        ? mission
        : throw new KeyNotFoundException($"Unknown mission: {id}");

    public MonsterConfig GetMonster(string id) => _monsters.TryGetValue(id, out var monster)
        ? monster
        : throw new KeyNotFoundException($"Unknown monster: {id}");

    public CombatDangerConfig GetDanger(int level) => Combat.DangerLevels.FirstOrDefault(value => value.Level == level)
        ?? throw new KeyNotFoundException($"Unknown danger level: {level}");

    public CombatBackgroundConfig GetCombatBackground(string id) => _backgrounds.TryGetValue(id, out var background)
        ? background
        : throw new KeyNotFoundException($"Unknown combat background: {id}");

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
        if (Combat.RenderWidth <= 0 || Combat.RenderHeight <= 0 || Combat.HeroBaseHealth <= 0m ||
            Combat.HealthRegenerationPerSecond < 0m ||
            Combat.HeroAttacksPerSecond <= 0f || Combat.RecoveryHealthFraction is <= 0m or > 1m)
            throw new InvalidDataException("Combat settings are invalid.");
        if (Dog.ChargeDurationSeconds <= 0f || Dog.RewardUnitRubles <= 0 ||
            Dog.MinimumRewardUnits <= 0 || Dog.MaximumRewardUnits < Dog.MinimumRewardUnits ||
            Dog.MaximumRewardUnits == int.MaxValue ||
            Dog.BaseScale <= 0f || Dog.MinimumChargeScale <= 0f ||
            Dog.MaximumChargeScale < Dog.MinimumChargeScale || Dog.GlowScale <= 0f ||
            Dog.BobAmplitude < 0f || Dog.BobSpeed < 0f || Dog.SwaySpeed < 0f ||
            Dog.BreathingAmplitude is < 0f or >= 1f || Dog.BreathingSpeed < 0f ||
            Dog.GlowPulseAmplitude is < 0f or >= 1f || Dog.GlowPulseSpeed < 0f ||
            Dog.GlowRed is < 0f or > 1f || Dog.GlowGreen is < 0f or > 1f ||
            Dog.GlowBlue is < 0f or > 1f || Dog.GlowAlpha is < 0f or > 1f ||
            string.IsNullOrWhiteSpace(Dog.MeditatingTexture) || string.IsNullOrWhiteSpace(Dog.ChargedTexture))
            throw new InvalidDataException("Dog meditation settings are invalid.");

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
            if (mission.DangerLevel is { } danger)
            {
                _ = GetDanger(danger);
                if (mission.PossibleMonsterIds.Count == 0 || mission.PossibleBackgroundIds.Count == 0)
                    throw new InvalidDataException($"Dangerous mission has no combat pool: {mission.Id}");
                foreach (var monster in mission.PossibleMonsterIds)
                    _ = GetMonster(monster);
                foreach (var background in mission.PossibleBackgroundIds)
                    _ = GetCombatBackground(background);
            }
        }

        foreach (var monster in _monsters.Values)
            if (monster.MaximumHealth <= 0m || monster.Attack <= 0m || monster.Defense < 0m ||
                monster.AttacksPerSecond <= 0f || monster.SelectionWeight <= 0m || string.IsNullOrWhiteSpace(monster.SpriteSet))
                throw new InvalidDataException($"Invalid monster balance: {monster.Id}");
    }

    private static MonstersConfig CreateDefaultMonsters() => new()
    {
        Monsters = [new MonsterConfig { Id = "training_spirit", Name = "Учебный дух", SpriteSet = "Textures/Characters/1 Samurai/Samurai" }]
    };

    private static CombatConfig CreateDefaultCombat() => new()
    {
        DangerLevels = [new CombatDangerConfig { Level = 1, EncounterChancePercent = 100m }],
        Backgrounds = [new CombatBackgroundConfig { Id = "forest", Layers = ["Textures/Backgrounds/1/orig.png"] }]
    };

    private static void AddUnique<T>(IDictionary<string, T> target, string id, T value, string kind)
    {
        if (string.IsNullOrWhiteSpace(id) || !target.TryAdd(id, value))
            throw new InvalidDataException($"Invalid or duplicate {kind} id: {id}");
    }
}
