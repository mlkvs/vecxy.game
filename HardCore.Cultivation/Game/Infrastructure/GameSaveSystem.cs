using System.Text.Json;
using System.Text.Json.Serialization;
using HardCore.Cultivation.Game.Domain;
using Vecxy.Diagnostics;
using GameState = HardCore.Cultivation.Game.Domain.GameState;

namespace HardCore.Cultivation.Game.Infrastructure;

public sealed class GameSaveSystem(GameDatabase database)
{
    public const int CurrentVersion = 8;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public string SavePath { get; set; } = GetDefaultSavePath();

    public void Save(GameState state)
    {
        var data = new SaveData
        {
            Version = CurrentVersion,
            TotalTicks = state.Calendar.TotalTicks,
            ActivityMode = state.ActivityMode,
            RecoveryRequired = state.RecoveryRequired,
            DogMeditationSeconds = state.DogMeditation.ElapsedSeconds,
            Character = new CharacterSaveData
            {
                SpiritualPower = state.Character.SpiritualPower,
                Money = state.Character.Money,
                TotalYears = state.Character.Age.TotalYears,
                StageIndex = state.Character.Cultivation.StageIndex,
                Level = state.Character.Cultivation.Level,
                Health = state.Character.Health,
                MaximumHealth = state.Character.MaximumHealth
            },
            Inventory = state.Inventory.Items.Select(ToItemData).ToList(),
            MissionQueue = state.MissionQueue.Select(mission => new MissionSaveData
            {
                InstanceId = mission.InstanceId,
                ConfigId = mission.MissionConfigId,
                RequiredProgress = mission.RequiredProgress,
                CurrentProgress = mission.CurrentProgress,
                RewardGranted = mission.RewardGranted,
                Rewards = mission.Rewards.Select(ToRewardData).ToList(),
                Encounter = mission.Encounter is null ? null : new MissionEncounterSaveData
                {
                    MonsterConfigId = mission.Encounter.MonsterConfigId,
                    BackgroundId = mission.Encounter.BackgroundId,
                    DangerLevel = mission.Encounter.DangerLevel,
                    TriggerProgress = mission.Encounter.TriggerProgress,
                    Resolved = mission.Encounter.Resolved
                },
                Combat = mission.Combat is null ? null : new CombatSaveData
                {
                    MonsterConfigId = mission.Combat.MonsterConfigId,
                    BackgroundId = mission.Combat.BackgroundId,
                    DangerLevel = mission.Combat.DangerLevel,
                    EnemyMaximumHealth = mission.Combat.EnemyMaximumHealth,
                    EnemyHealth = mission.Combat.EnemyHealth,
                    HeroCooldown = mission.Combat.HeroCooldown,
                    EnemyCooldown = mission.Combat.EnemyCooldown,
                    Phase = mission.Combat.Phase,
                    FinishDelay = mission.Combat.FinishDelay
                }
            }).ToList(),
            AvailableMissionIds = state.MissionBoard.MissionIds.ToList(),
            Shop = new ShopSaveData
            {
                BuyMarkupPercent = state.Shop.BuyMarkupPercent,
                SellAdjustmentPercent = state.Shop.SellAdjustmentPercent,
                Slots = state.Shop.Slots.Select(slot => new ShopSlotSaveData
                {
                    SlotId = slot.SlotId,
                    Item = ToItemData(slot.Item),
                    AvailableQuantity = slot.AvailableQuantity
                }).ToList()
            },
            ActiveEffects = state.ActiveEffects.Select(effect => new EffectSaveData
            {
                SourceItemId = effect.SourceItemId,
                Type = effect.Type,
                Operation = effect.Operation,
                Value = effect.Value,
                SourceRarity = effect.SourceRarity,
                SourceQuality = effect.SourceQuality,
                RemainingTicks = effect.RemainingTicks,
                DurationType = effect.DurationType
            }).ToList()
        };

        var directory = Path.GetDirectoryName(SavePath);
        if (!string.IsNullOrWhiteSpace(directory))
            Directory.CreateDirectory(directory);
        var temporaryPath = SavePath + ".tmp";
        File.WriteAllText(temporaryPath, JsonSerializer.Serialize(data, JsonOptions));
        File.Move(temporaryPath, SavePath, true);
    }

    public bool TryLoad(out GameState state)
    {
        state = new GameState(database.Balance.TicksPerYear);
        var loadPath = ResolveLoadPath();
        if (loadPath is null)
            return false;
        try
        {
            var data = JsonSerializer.Deserialize<SaveData>(
                File.ReadAllText(loadPath),
                JsonOptions);
            if (data is null || data.Version is < 2 or > CurrentVersion)
                return false;

            state.Calendar.Restore(data.TotalTicks);
            state.SetActivityMode(data.ActivityMode);
            state.Character.Restore(
                data.Character.SpiritualPower,
                data.Character.Money,
                data.Character.TotalYears);
            state.Character.Cultivation.Restore(
                data.Character.StageIndex,
                data.Character.Level,
                database.Cultivation.Stages.Count);
            if (data.Version >= 6 && data.Character.MaximumHealth > 0m)
                state.Character.RestoreHealth(data.Character.Health, data.Character.MaximumHealth);
            else
                state.Character.ConfigureMaximumHealth(database.Combat.HeroBaseHealth, true);
            state.RestoreDefeatRecovery(
                data.Version >= 7 && data.RecoveryRequired &&
                state.Character.Health < database.Combat.RecoveryHealthFraction * state.Character.MaximumHealth);
            state.DogMeditation.Restore(
                data.Version >= 8 ? data.DogMeditationSeconds : 0f,
                database.Dog.ChargeDurationSeconds);
            state.Inventory.ReplaceWith(data.Inventory.Select(FromItemData));
            state.ActiveEffects.AddRange(data.ActiveEffects.Select(effect =>
            {
                var active = new ActiveEffect
                {
                    SourceItemId = effect.SourceItemId,
                    Type = effect.Type,
                    Operation = effect.Operation,
                    Value = effect.Value,
                    SourceRarity = effect.SourceRarity,
                    SourceQuality = effect.SourceQuality <= 0m ? 2.5m : effect.SourceQuality,
                    DurationType = data.Version >= 5
                        ? effect.DurationType
                        : effect.RemainingTicks is null
                            ? ItemDurationType.Permanent
                            : ItemDurationType.Temporary
                };
                active.RestoreDuration(effect.RemainingTicks);
                return active;
            }));

            var savedMissions = data.MissionQueue.Count > 0
                ? data.MissionQueue
                : data.Mission is null ? [] : [data.Mission];
            foreach (var savedMission in savedMissions)
            {
                _ = database.GetMission(savedMission.ConfigId);
                var mission = new ActiveMission
                {
                    InstanceId = savedMission.InstanceId == Guid.Empty ? Guid.NewGuid() : savedMission.InstanceId,
                    MissionConfigId = savedMission.ConfigId,
                    RequiredProgress = savedMission.RequiredProgress,
                    Rewards = savedMission.Rewards.Count > 0
                        ? savedMission.Rewards.Select(FromRewardData).ToList()
                        : LegacyRewards(savedMission.ConfigId),
                    Encounter = RestoreEncounter(savedMission.Encounter)
                };
                mission.Restore(savedMission.CurrentProgress, savedMission.RewardGranted);
                mission.RestoreCombat(RestoreCombat(savedMission.Combat));
                state.EnqueueMission(mission);
            }
            foreach (var missionId in data.AvailableMissionIds)
                _ = database.GetMission(missionId);
            state.MissionBoard.ReplaceWith(data.AvailableMissionIds);

            var slots = data.Shop.Slots.Select(slot =>
            {
                var restored = new ShopSlot
                {
                    SlotId = slot.SlotId,
                    Item = FromItemData(slot.Item)
                };
                restored.RestoreQuantity(slot.AvailableQuantity);
                return restored;
            });
            state.Shop.ReplaceStock(
                slots,
                data.Shop.BuyMarkupPercent,
                database.Shop.SellAdjustmentPercent);
            return true;
        }
        catch (Exception exception)
        {
            Logger.Error(exception, $"Could not load save: {loadPath}");
            state = new GameState(database.Balance.TicksPerYear);
            return false;
        }
    }

    private static string GetDefaultSavePath()
    {
        var baseDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Vecxy",
            "HardCore.Cultivation");
        return Path.Combine(baseDirectory, "cultivation-save-v2.json");
    }

    private string? ResolveLoadPath()
    {
        if (File.Exists(SavePath))
            return SavePath;

        var legacyPath = Path.Combine(AppContext.BaseDirectory, "cultivation-save-v2.json");
        return File.Exists(legacyPath) ? legacyPath : null;
    }

    private ItemInstance FromItemData(ItemSaveData data)
    {
        _ = database.GetItem(data.ConfigId);
        var item = new ItemInstance
        {
            InstanceId = data.InstanceId,
            ConfigId = data.ConfigId,
            Rarity = data.Rarity,
            Quality = data.Quality
        };
        item.RestoreQuantity(data.Quantity);
        return item;
    }

    private static ItemSaveData ToItemData(ItemInstance item) => new()
    {
        InstanceId = item.InstanceId,
        ConfigId = item.ConfigId,
        Rarity = item.Rarity,
        Quality = item.Quality,
        Quantity = item.Quantity
    };

    private List<MissionReward> LegacyRewards(string missionId)
    {
        var money = database.GetMission(missionId).Reward.Money;
        return money > 0
            ? [new MissionReward { Type = MissionRewardType.Money, Money = money }]
            : [];
    }

    private static MissionReward FromRewardData(MissionRewardSaveData reward) => new()
    {
        Type = reward.Type,
        Money = reward.Money,
        ItemConfigId = reward.ItemConfigId,
        ItemRarity = reward.ItemRarity,
        ItemQuality = reward.ItemQuality,
        Quantity = reward.Quantity
    };

    private static MissionRewardSaveData ToRewardData(MissionReward reward) => new()
    {
        Type = reward.Type,
        Money = reward.Money,
        ItemConfigId = reward.ItemConfigId,
        ItemRarity = reward.ItemRarity,
        ItemQuality = reward.ItemQuality,
        Quantity = reward.Quantity
    };

    private MissionEncounter? RestoreEncounter(MissionEncounterSaveData? data)
    {
        if (data is null)
            return null;
        _ = database.GetMonster(data.MonsterConfigId);
        _ = database.GetCombatBackground(data.BackgroundId);
        var encounter = new MissionEncounter
        {
            MonsterConfigId = data.MonsterConfigId,
            BackgroundId = data.BackgroundId,
            DangerLevel = data.DangerLevel,
            TriggerProgress = data.TriggerProgress
        };
        encounter.RestoreResolved(data.Resolved);
        return encounter;
    }

    private ActiveCombat? RestoreCombat(CombatSaveData? data)
    {
        if (data is null)
            return null;
        _ = database.GetMonster(data.MonsterConfigId);
        _ = database.GetCombatBackground(data.BackgroundId);
        var combat = new ActiveCombat
        {
            MonsterConfigId = data.MonsterConfigId,
            BackgroundId = data.BackgroundId,
            DangerLevel = data.DangerLevel,
            EnemyMaximumHealth = data.EnemyMaximumHealth
        };
        combat.Restore(data.EnemyHealth, data.HeroCooldown, data.EnemyCooldown, data.Phase, data.FinishDelay);
        return combat;
    }
}

public sealed class SaveData
{
    public int Version { get; init; }
    public long TotalTicks { get; init; }
    public ActivityMode ActivityMode { get; init; } = ActivityMode.Cultivation;
    public bool RecoveryRequired { get; init; }
    public float DogMeditationSeconds { get; init; }
    public CharacterSaveData Character { get; init; } = new();
    public List<ItemSaveData> Inventory { get; init; } = [];
    public List<MissionSaveData> MissionQueue { get; init; } = [];
    public List<string> AvailableMissionIds { get; init; } = [];
    // Kept for migration from version 2 saves.
    public MissionSaveData? Mission { get; init; }
    public ShopSaveData Shop { get; init; } = new();
    public List<EffectSaveData> ActiveEffects { get; init; } = [];
}

public sealed class CharacterSaveData
{
    public decimal SpiritualPower { get; init; }
    public long Money { get; init; }
    public decimal TotalYears { get; init; }
    public int StageIndex { get; init; }
    public int Level { get; init; } = 1;
    public decimal Health { get; init; }
    public decimal MaximumHealth { get; init; }
}

public sealed class ItemSaveData
{
    public Guid InstanceId { get; init; }
    public string ConfigId { get; init; } = string.Empty;
    public ItemRarity Rarity { get; init; }
    public decimal Quality { get; init; }
    public int Quantity { get; init; } = 1;
}

public sealed class MissionSaveData
{
    public Guid InstanceId { get; init; }
    public string ConfigId { get; init; } = string.Empty;
    public decimal RequiredProgress { get; init; }
    public decimal CurrentProgress { get; init; }
    public bool RewardGranted { get; init; }
    public List<MissionRewardSaveData> Rewards { get; init; } = [];
    public MissionEncounterSaveData? Encounter { get; init; }
    public CombatSaveData? Combat { get; init; }
}

public sealed class MissionEncounterSaveData
{
    public string MonsterConfigId { get; init; } = string.Empty;
    public string BackgroundId { get; init; } = string.Empty;
    public int DangerLevel { get; init; }
    public decimal TriggerProgress { get; init; }
    public bool Resolved { get; init; }
}

public sealed class CombatSaveData
{
    public string MonsterConfigId { get; init; } = string.Empty;
    public string BackgroundId { get; init; } = string.Empty;
    public int DangerLevel { get; init; }
    public decimal EnemyMaximumHealth { get; init; }
    public decimal EnemyHealth { get; init; }
    public float HeroCooldown { get; init; }
    public float EnemyCooldown { get; init; }
    public CombatPhase Phase { get; init; }
    public float FinishDelay { get; init; }
}

public sealed class MissionRewardSaveData
{
    public MissionRewardType Type { get; init; }
    public long Money { get; init; }
    public string? ItemConfigId { get; init; }
    public ItemRarity ItemRarity { get; init; }
    public decimal ItemQuality { get; init; }
    public int Quantity { get; init; } = 1;
}

public sealed class ShopSaveData
{
    public int BuyMarkupPercent { get; init; }
    public int SellAdjustmentPercent { get; init; }
    public List<ShopSlotSaveData> Slots { get; init; } = [];
}

public sealed class ShopSlotSaveData
{
    public Guid SlotId { get; init; }
    public ItemSaveData Item { get; init; } = new();
    public int AvailableQuantity { get; init; }
}

public sealed class EffectSaveData
{
    public string SourceItemId { get; init; } = string.Empty;
    public EffectType Type { get; init; }
    public ModifierOperation Operation { get; init; }
    public decimal Value { get; init; }
    public ItemRarity SourceRarity { get; init; }
    public decimal SourceQuality { get; init; } = 2.5m;
    public int? RemainingTicks { get; init; }
    public ItemDurationType DurationType { get; init; } = ItemDurationType.Temporary;
}
