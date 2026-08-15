using HardCore.Cultivation.Game.Domain;
using HardCore.Cultivation.Game.Application;
using System.Reflection;
using Vecxy.Assets;

namespace HardCore.Cultivation.Game.Infrastructure;

// Public build fields shared by the build script and the game. Signing data is intentionally absent.
public sealed class BuildConfig : IYamlConfig
{
    public BuildTargetConfig Build { get; init; } = new();
    public BuildGameConfig Game { get; init; } = new();
    public GooglePlayBuildConfig GooglePlay { get; init; } = new();
}

public sealed class BuildTargetConfig
{
    public string Platform { get; init; } = string.Empty;
    public string DefinesCommon { get; init; } = string.Empty;
    public string DefinesAndroid { get; init; } = string.Empty;
    public string DefinesDesktop { get; init; } = string.Empty;
    public string DefinesDev { get; init; } = string.Empty;
    public string DefinesRelease { get; init; } = string.Empty;
}

public sealed class BuildGameConfig
{
    public string Name { get; init; } = "HardCore Cultivation";
    public string Version { get; init; } = "1.0.0";
    public string Icon { get; init; } = string.Empty;
}

public sealed class GooglePlayBuildConfig
{
    public int VersionCode { get; init; }
    public string BundleVersion { get; init; } = "1.0.0";
}

public sealed class AnalyticsConfig : IYamlConfig
{
    public AppMetricaConfig AppMetrica { get; init; } = new();
}

public sealed class AppMetricaConfig
{
    public string ApiKey { get; init; } = string.Empty;
}

public sealed class GameBuildInfo
{
    public string Platform { get; private set; } = "desktop";
    public string Name { get; private set; } = "HardCore Cultivation";
    public string Version { get; private set; } = "1.0.0";
    public int VersionCode { get; private set; }
    public string BundleVersion { get; private set; } = "1.0.0";
    public string Defines { get; private set; } = string.Empty;
    public string DisplayVersion => VersionCode > 0 ? $"{Version} #{VersionCode}" : Version;

    public void Initialize(BuildConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);

        Platform = config.Build.Platform;
        Name = config.Game.Name;
        Version = config.Game.Version;
        VersionCode = config.GooglePlay.VersionCode;
        BundleVersion = config.GooglePlay.BundleVersion;
        var platformDefines = config.Build.Platform.Equals("android", StringComparison.OrdinalIgnoreCase)
            ? config.Build.DefinesAndroid
            : config.Build.DefinesDesktop;
#if DEBUG
        Defines = JoinDefines(config.Build.DefinesCommon, platformDefines, config.Build.DefinesDev);
#else
        Defines = JoinDefines(config.Build.DefinesCommon, platformDefines, config.Build.DefinesRelease);
#endif
    }

    public void InitializeFromAssembly()
    {
        var metadata = typeof(GameBuildInfo).Assembly
            .GetCustomAttributes<System.Reflection.AssemblyMetadataAttribute>()
            .ToDictionary(attribute => attribute.Key, attribute => attribute.Value ?? string.Empty, StringComparer.Ordinal);

        Platform = Get(metadata, "BuildPlatform", "android");
        Name = Get(metadata, "BuildGameName", Name);
        Version = Get(metadata, "BuildGameVersion", Version);
        BundleVersion = Get(metadata, "BuildBundleVersion", Version);
        Defines = Get(metadata, "BuildDefines", string.Empty);
        VersionCode = int.TryParse(Get(metadata, "BuildGooglePlayVersionCode", "0"), out var code) ? code : 0;
    }

    private static string JoinDefines(params string[] values) => string.Join(",", values.Where(value => !string.IsNullOrWhiteSpace(value)));
    private static string Get(IReadOnlyDictionary<string, string> metadata, string key, string fallback) =>
        metadata.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value) ? value : fallback;
}

public sealed class GameAnalyticsInfo
{
    public string AppMetricaApiKey { get; private set; } = string.Empty;
    public bool IsAppMetricaEnabled => !string.IsNullOrWhiteSpace(AppMetricaApiKey);

    public void Initialize(AnalyticsConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);
        AppMetricaApiKey = config.AppMetrica.ApiKey;
    }

    public void InitializeFromAssembly()
    {
        AppMetricaApiKey = typeof(GameAnalyticsInfo).Assembly
            .GetCustomAttributes<System.Reflection.AssemblyMetadataAttribute>()
            .FirstOrDefault(attribute => attribute.Key == "AppMetricaApiKey")?.Value ?? string.Empty;
    }
}

public sealed class GameBalanceConfig : IYamlConfig
{
    public int TicksPerYear { get; init; } = 48;
    public int RealMillisecondsPerTick { get; init; } = 1000;
    public int AutoSaveEveryTicks { get; init; } = 12;
    public decimal BaseSpiritualPowerPerTick { get; init; } = 1m;
    public decimal MinimumTickEfficiency { get; init; } = 0.1m;
    public decimal MinimumAgingMultiplier { get; init; } = 0m;
    public decimal MaximumBreakthroughChance { get; init; } = 100m;
    public long StartingMoney { get; init; } = 1000;
    public decimal StartingAgeYears { get; init; } = 16m;
    public CharacterStats InitialCharacterStats { get; init; } = new(100m, 1m, 1m, 1m, 80m);
    public int MaximumMissionQueueSize { get; init; } = 6;
    public List<QualityBand> QualityBands { get; init; } = [];
    public List<ContaminationBand> ContaminationBands { get; init; } = [];
    public decimal ContaminationCombinationDivisor { get; init; } = 3m;
    public decimal ContaminationAbsorptionPerPill { get; init; } = 1m;
    public List<ContaminationLevelConfig> ContaminationLevels { get; init; } = [];
    public List<PriceCurvePoint> QualityPriceCurve { get; init; } = [];
    public Dictionary<ItemCategory, PriceCurvePoint> LowQualityPriceMultipliers { get; init; } = [];
}

public sealed class QualityBand
{
    public int Index { get; init; }
    public decimal Weight { get; init; }
}

public sealed class ContaminationBand
{
    public decimal Minimum { get; init; }
    public decimal Maximum { get; init; }
    public decimal Weight { get; init; }
}

public sealed class ContaminationLevelConfig
{
    public decimal MinimumContamination { get; init; }
    public string Name { get; init; } = string.Empty;
    public List<ItemEffectDefinition> Effects { get; init; } = [];
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
    public Element? Element { get; init; }
    public long BasePrice { get; init; }
    public int TemporaryDurationTicks { get; init; }
    public decimal ShopWeight { get; init; } = 1m;
    public List<ItemEffectDefinition> Effects { get; init; } = [];
    public List<AlchemyPropertyAmount> AlchemyProperties { get; init; } = [];
}

public sealed class AlchemyConfig : IYamlConfig
{
    public bool Enabled { get; init; }
    public string CraftedPillItemId { get; init; } = "crafted_alchemy_pill";
    public string PurityPillItemId { get; init; } = "purity_pill";
    public string ExtractItemId { get; init; } = "alchemy_extract";
    public int MinimumIngredients { get; init; } = 2;
    public int MaximumIngredients { get; init; } = 6;
    public int MinimumPropertyMatches { get; init; } = 2;
    public decimal MinimumPropertyFraction { get; init; } = 0.6m;
    public int MaximumPillEffects { get; init; } = 4;
    public int PillDurationTicks { get; init; } = 48;
    public List<AlchemyOutputQuantityChance> PillOutputQuantityChances { get; init; } =
    [
        new() { Quantity = 1, ChancePercent = 45m },
        new() { Quantity = 2, ChancePercent = 25m },
        new() { Quantity = 3, ChancePercent = 15m },
        new() { Quantity = 4, ChancePercent = 8m },
        new() { Quantity = 5, ChancePercent = 5m },
        new() { Quantity = 6, ChancePercent = 2m }
    ];
    public decimal ResultAverageWeight { get; init; } = 0.6m;
    public decimal ResultMaximumWeight { get; init; } = 0.4m;
    public decimal CoreRankWeight { get; init; } = 1.5m;
    public decimal QualityRandomnessSigma { get; init; } = 0.30m;
    public decimal RarityRandomnessSigma { get; init; } = 0.40m;
    public decimal RandomnessReferenceIngredientCount { get; init; } = 2m;
    public decimal MaximumQuality { get; init; } = 5m;
    public decimal DistillationQualityPerIngredient { get; init; } = 0.12m;
    public decimal DistillationQualityPerLevel { get; init; } = 0.18m;
    public decimal CraftSuccessChancePerQuality { get; init; } = 10m;
    public decimal MaximumCraftSuccessChance { get; init; } = 95m;
    public string PurificationPropertyId { get; init; } = "purification";
    public decimal PurificationMixedRecipeChance { get; init; } = 0.5m;
    public decimal PurificationMinimumPercent { get; init; } = 25m;
    public decimal PurificationMaximumPercent { get; init; } = 55m;
    public decimal ElementCompatibilityCoefficient { get; init; } = 0.15m;
    public Dictionary<Element, Dictionary<Element, decimal>> ElementCompatibility { get; init; } = [];
    public List<ContaminationCurvePoint> ContaminationModifierCurve { get; init; } = [];
    public List<AlchemyPropertyConfig> Properties { get; init; } = [];
}

public sealed class AlchemyOutputQuantityChance
{
    public int Quantity { get; init; }
    public decimal ChancePercent { get; init; }
}

public sealed class ContaminationCurvePoint
{
    public decimal Contamination { get; init; }
    public decimal Multiplier { get; init; }
}

public sealed class AlchemyPropertyConfig
{
    public string Id { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
    public string PillName { get; init; } = string.Empty;
    public EffectType EffectType { get; init; }
    public ModifierOperation Operation { get; init; }
    public decimal BaseValue { get; init; }
}

public sealed class MissionsConfig : IYamlConfig
{
    public int BoardSlotCount { get; init; } = 6;
    public List<MissionConfig> Missions { get; init; } = [];
}

public sealed class MissionConfig
{
    public string Id { get; init; } = string.Empty;
    // The board offers only the player's stage and its adjacent stage pools.
    public string StageId { get; init; } = string.Empty;
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
    public decimal RecoveryHealthPoints { get; init; } = 30m;
    public float FinishDelaySeconds { get; init; } = 1.2f;
    public List<CombatDangerConfig> DangerLevels { get; init; } = [];
    public List<CombatBackgroundConfig> Backgrounds { get; init; } = [];
}

public sealed class CombatDangerConfig
{
    public int Level { get; init; }
    public string Name { get; init; } = string.Empty;
    public decimal EncounterChancePercent { get; init; }
    public StageStatReference StatReference { get; init; }
    public decimal StatMultiplier { get; init; } = 1m;
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
    public string MissionMeditatingTexture { get; init; } = "Textures/Dog_Missions.png";
    public string MissionChargedTexture { get; init; } = "Textures/Dog_Missions2.png";
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
    public List<decimal> InitialRequiredPower { get; init; } = [];
    public decimal StageEntryCoefficient { get; init; } = 0.7m;
    public decimal BreakthroughChancePerExtraPowerBar { get; init; } = 10m;
    public List<CultivationStageConfig> Stages { get; init; } = [];
}

public sealed class CultivationStageConfig
{
    public string Id { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string CultivationBackgroundTexture { get; init; } = "Textures/Background.jpg";
    public string MissionBackgroundTexture { get; init; } = "Textures/Background_Missions.jpg";
    public decimal RecursiveCoefficient { get; init; }
    public decimal SpiritualPowerMultiplier { get; init; } = 1m;
    public CharacterStats StatsPerLevel { get; init; }
    public CharacterStats BreakthroughBonus { get; init; }
    public decimal BaseBreakthroughChance { get; init; }
}

public sealed class ShopConfig : IYamlConfig
{
    public int SlotCount { get; init; } = 6;
    public int MinimumQuantity { get; init; } = 1;
    public int MaximumQuantity { get; init; } = 1;
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
    private readonly Dictionary<string, AlchemyPropertyConfig> _alchemyProperties = new(StringComparer.Ordinal);
    private readonly Dictionary<ItemRarity, RarityConfig> _rarities = [];

    public GameBalanceConfig Balance { get; private set; } = new();
    public CultivationConfig Cultivation { get; private set; } = new();
    public ShopConfig Shop { get; private set; } = new();
    public CombatConfig Combat { get; private set; } = new();
    public DogConfig Dog { get; private set; } = new();
    public AlchemyConfig Alchemy { get; private set; } = new();
    public CultivationBalanceSnapshot CultivationBalance { get; private set; } = null!;
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
        ConfigRef<DogConfig> dog,
        ConfigRef<AlchemyConfig> alchemy)
        => Initialize(balance.Value, rarities.Value, items.Value, missions.Value, cultivation.Value, shop.Value,
            monsters.Value, combat.Value, dog.Value, alchemy.Value);

    public void Initialize(
        GameBalanceConfig balance,
        RaritiesConfig rarities,
        ItemsConfig items,
        MissionsConfig missions,
        CultivationConfig cultivation,
        ShopConfig shop,
        MonstersConfig? monsters = null,
        CombatConfig? combat = null,
        DogConfig? dog = null,
        AlchemyConfig? alchemy = null)
    {
        Balance = balance;
        Cultivation = cultivation;
        Shop = shop;
        Combat = combat ?? CreateDefaultCombat();
        Dog = dog ?? new DogConfig();
        Alchemy = alchemy ?? CreateDefaultAlchemy();
        MissionBoardSlotCount = missions.BoardSlotCount;
        _items.Clear();
        _missions.Clear();
        _rarities.Clear();
        _monsters.Clear();
        _backgrounds.Clear();
        _alchemyProperties.Clear();

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
        foreach (var property in Alchemy.Properties)
            AddUnique(_alchemyProperties, property.Id, property, "alchemy property");

        Validate();
        CultivationBalance = new CultivationBalanceSnapshot(Balance, Cultivation);
    }

    public ItemConfig GetItem(string id) => _items.TryGetValue(id, out var item)
        ? item
        : throw new KeyNotFoundException($"Unknown item: {id}");

    public MissionConfig GetMission(string id) => _missions.TryGetValue(id, out var mission)
        ? mission
        : throw new KeyNotFoundException($"Unknown mission: {id}");

    public int GetCultivationStageIndex(string id)
    {
        var index = Cultivation.Stages.FindIndex(stage => stage.Id == id);
        return index >= 0 ? index : throw new KeyNotFoundException($"Unknown cultivation stage: {id}");
    }

    public int GetMissionBoardCapacityForStage(int currentStageIndex)
    {
        var availableMissionCount = _missions.Values.Count(mission =>
            Math.Abs(GetCultivationStageIndex(mission.StageId) - currentStageIndex) <= 1);
        return Math.Max(1, Math.Min(MissionBoardSlotCount, availableMissionCount));
    }

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

    public AlchemyPropertyConfig GetAlchemyProperty(string id) =>
        _alchemyProperties.TryGetValue(id, out var property)
            ? property
            : throw new KeyNotFoundException($"Unknown alchemy property: {id}");

    private void Validate()
    {
        if (Balance.TicksPerYear <= 0 || Balance.RealMillisecondsPerTick <= 0)
            throw new InvalidDataException("Tick settings must be positive.");
        if (Balance.InitialCharacterStats.LongevityYears <= Balance.StartingAgeYears || Balance.MaximumMissionQueueSize <= 0 ||
            Balance.InitialCharacterStats.MaximumHealth <= 0m || Balance.InitialCharacterStats.HealthRegeneration < 0m ||
            Balance.InitialCharacterStats.Attack < 0m || Balance.InitialCharacterStats.AttacksPerSecond <= 0m ||
            Balance.InitialCharacterStats.LongevityYears <= 0m)
            throw new InvalidDataException("Lifetime and mission queue settings are invalid.");
        if (Balance.QualityBands.Count == 0 || Balance.QualityBands.Any(band => band.Index is < 1 or > 5 || band.Weight <= 0m))
            throw new InvalidDataException("Quality bands are invalid.");
        if (Balance.ContaminationBands.Count == 0 || Balance.ContaminationBands.Any(band =>
                band.Minimum is < 0m or > 1m || band.Maximum is < 0m or > 1m || band.Maximum < band.Minimum || band.Weight <= 0m) ||
                Balance.ContaminationAbsorptionPerPill < 0m || Balance.ContaminationCombinationDivisor <= 0m)
            throw new InvalidDataException("Contamination generation settings are invalid.");
        if (Balance.ContaminationLevels.Count != 4 || Balance.ContaminationLevels.Any(level =>
                level.MinimumContamination is <= 0m or > 1m || string.IsNullOrWhiteSpace(level.Name)) ||
            Balance.ContaminationLevels.OrderBy(level => level.MinimumContamination)
                .Select(level => level.MinimumContamination).Distinct().Count() != Balance.ContaminationLevels.Count)
            throw new InvalidDataException("Contamination requires four uniquely-thresholded levels.");
        if (Balance.QualityPriceCurve.Count < 2)
            throw new InvalidDataException("Quality price curve requires at least two points.");
        if (_rarities.Count != Enum.GetValues<ItemRarity>().Length)
            throw new InvalidDataException("Every item rarity must be configured.");
        if (Cultivation.InitialRequiredPower.Count != 2 || Cultivation.InitialRequiredPower.Any(value => value <= 0m) ||
            Cultivation.StageEntryCoefficient <= 0m || Cultivation.BreakthroughChancePerExtraPowerBar < 0m ||
            Cultivation.Stages.Count == 0 || Cultivation.Stages.Select(stage => stage.Id).Distinct(StringComparer.Ordinal).Count() != Cultivation.Stages.Count ||
            Cultivation.Stages.Any(stage => stage.RecursiveCoefficient <= 0m || stage.SpiritualPowerMultiplier <= 0m))
            throw new InvalidDataException("Cultivation coefficients and initial costs are invalid.");
        if (Cultivation.Stages.Any(stage =>
                string.IsNullOrWhiteSpace(stage.CultivationBackgroundTexture) ||
                string.IsNullOrWhiteSpace(stage.MissionBackgroundTexture)))
            throw new InvalidDataException("Every cultivation stage must define cultivation and mission backgrounds.");
        if (_items.Count == 0 || _missions.Count == 0)
            throw new InvalidDataException("Items and missions cannot be empty.");
        if (MissionBoardSlotCount <= 0 || MissionBoardSlotCount > _missions.Count)
            throw new InvalidDataException("Mission board slot count is invalid.");
        if (Combat.RenderWidth <= 0 || Combat.RenderHeight <= 0 || Combat.HeroBaseHealth <= 0m ||
            Combat.HealthRegenerationPerSecond < 0m ||
            Combat.HeroAttacksPerSecond <= 0f || Combat.RecoveryHealthPoints <= 0m)
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
            string.IsNullOrWhiteSpace(Dog.MeditatingTexture) || string.IsNullOrWhiteSpace(Dog.ChargedTexture) ||
            string.IsNullOrWhiteSpace(Dog.MissionMeditatingTexture) ||
            string.IsNullOrWhiteSpace(Dog.MissionChargedTexture))
            throw new InvalidDataException("Dog meditation settings are invalid.");
        if (Alchemy.Enabled && (Alchemy.MinimumIngredients <= 0 || Alchemy.MaximumIngredients < Alchemy.MinimumIngredients ||
            Alchemy.MinimumPropertyMatches <= 0 || Alchemy.MinimumPropertyMatches > Alchemy.MaximumIngredients ||
            Alchemy.MinimumPropertyFraction is <= 0m or > 1m || Alchemy.MaximumPillEffects is < 1 or > 4 ||
            Alchemy.PillDurationTicks <= 0 || Alchemy.MaximumQuality <= 0m ||
            Alchemy.DistillationQualityPerIngredient < 0m || Alchemy.DistillationQualityPerLevel < 0m ||
            Alchemy.CraftSuccessChancePerQuality < 0m || Alchemy.MaximumCraftSuccessChance is < 0m or > 100m ||
            Alchemy.PurificationMixedRecipeChance is < 0m or > 1m || Alchemy.PurificationMinimumPercent is < 0m or > 100m ||
            Alchemy.PurificationMaximumPercent < Alchemy.PurificationMinimumPercent || Alchemy.PurificationMaximumPercent > 100m ||
            Alchemy.PillOutputQuantityChances.Count == 0 ||
            Alchemy.PillOutputQuantityChances.Any(chance => chance.Quantity <= 0 || chance.ChancePercent <= 0m) ||
            Alchemy.PillOutputQuantityChances.Sum(chance => chance.ChancePercent) != 100m ||
            Alchemy.PillOutputQuantityChances.Select(chance => chance.Quantity).Distinct().Count() != Alchemy.PillOutputQuantityChances.Count ||
            Alchemy.ResultAverageWeight < 0m || Alchemy.ResultMaximumWeight < 0m ||
            Alchemy.ResultAverageWeight + Alchemy.ResultMaximumWeight <= 0m ||
            Alchemy.CoreRankWeight <= 0m || Alchemy.QualityRandomnessSigma < 0m ||
            Alchemy.RarityRandomnessSigma < 0m || Alchemy.RandomnessReferenceIngredientCount <= 0m ||
            _alchemyProperties.Count == 0))
            throw new InvalidDataException("Alchemy settings are invalid.");
        if (Alchemy.Enabled)
        {
            _ = GetItem(Alchemy.CraftedPillItemId);
            _ = GetItem(Alchemy.PurityPillItemId);
            _ = GetItem(Alchemy.ExtractItemId);
            _ = GetAlchemyProperty(Alchemy.PurificationPropertyId);
        }
        if (Balance.QualityPriceCurve.OrderBy(point => point.Quality).Select(point => point.Quality).Distinct().Count() != Balance.QualityPriceCurve.Count ||
            Balance.QualityPriceCurve.Any(point => point.Multiplier <= 0m))
            throw new InvalidDataException("Quality price curve must have unique points with positive multipliers.");
        if (Alchemy.Enabled && (Alchemy.ContaminationModifierCurve.Count < 2 ||
            Alchemy.ContaminationModifierCurve.Any(point => point.Contamination is < 0m or > 1m || point.Multiplier <= 0m) ||
            Alchemy.ElementCompatibility.Any(row => row.Value.Any(pair => !Alchemy.ElementCompatibility.TryGetValue(pair.Key, out var reverse) || !reverse.TryGetValue(row.Key, out var value) || value != pair.Value))))
            throw new InvalidDataException("Alchemy compatibility matrix or contamination curve is invalid.");

        foreach (var item in _items.Values)
        {
            if (item.BasePrice < 0 || item.ShopWeight < 0m)
                throw new InvalidDataException($"Invalid item balance: {item.Id}");
            if (item.DurationType == ItemDurationType.Temporary && item.TemporaryDurationTicks <= 0)
                throw new InvalidDataException($"Temporary item has no duration: {item.Id}");
            if (item.AlchemyProperties.Count > 4)
                throw new InvalidDataException($"Item has more than four alchemy properties: {item.Id}");
            if (item.Effects.Any(effect => effect.Type is EffectType.HealthRestore or EffectType.PurifyContamination) &&
                item.DurationType != ItemDurationType.Instant)
                throw new InvalidDataException($"Instant-only effect is configured on a non-instant item: {item.Id}");
            foreach (var property in item.AlchemyProperties)
            {
                _ = GetAlchemyProperty(property.PropertyId);
            }
        }

        foreach (var mission in _missions.Values)
        {
            if (mission.MinimumDurationTicks <= 0 ||
                mission.MaximumDurationTicks < mission.MinimumDurationTicks ||
                mission.BoardWeight <= 0m ||
                string.IsNullOrWhiteSpace(mission.StageId))
            {
                throw new InvalidDataException($"Invalid mission balance: {mission.Id}");
            }
            _ = GetCultivationStageIndex(mission.StageId);
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
            if (monster.Defense < 0m || monster.SelectionWeight <= 0m || string.IsNullOrWhiteSpace(monster.SpriteSet))
                throw new InvalidDataException($"Invalid monster balance: {monster.Id}");
    }

    private static MonstersConfig CreateDefaultMonsters() => new()
    {
        Monsters = [new MonsterConfig { Id = "training_spirit", Name = "Учебный дух", SpriteSet = "Textures/Characters/1 Samurai/Samurai" }]
    };

    private static CombatConfig CreateDefaultCombat() => new()
    {
        DangerLevels = [new CombatDangerConfig { Level = 1, EncounterChancePercent = 100m }],
        Backgrounds = [new CombatBackgroundConfig { Id = "forest", Layers = ["Textures/Backgrounds/1/1.png"] }]
    };

    private static AlchemyConfig CreateDefaultAlchemy() => new();

    private static void AddUnique<T>(IDictionary<string, T> target, string id, T value, string kind)
    {
        if (string.IsNullOrWhiteSpace(id) || !target.TryAdd(id, value))
            throw new InvalidDataException($"Invalid or duplicate {kind} id: {id}");
    }
}
