namespace HardCore.Cultivation.Game.Domain;

public enum EffectType
{
    TickEfficiency,
    AgingSpeed,
    TimeAcceleration,
    BreakthroughChance,
    SpiritualPowerGain,
    MissionProgress,
    HealthRegeneration,
    MaximumHealth,
    Attack,
    AttackSpeed,
    HealthRestore,
    Contamination,
    LongevityYears,
    PurifyContamination
}

public enum Element { Fire, Water, Earth, Air, Void }
public enum StageStatReference { StageStart, StageEnd }

public enum ActivityMode
{
    Cultivation,
    Missions
}

public enum ModifierOperation
{
    Flat,
    AdditivePercent,
    MultiplicativePercent
}

public enum ItemCategory
{
    Pill,
    Core,
    Ingredient
}

public enum ItemDurationType
{
    Instant,
    Temporary,
    Permanent,
    UntilBreakthroughAttempt
}

public enum MissionRewardType
{
    Money,
    Item
}

public enum CombatPhase
{
    Fighting,
    Victory,
    Defeat
}

public enum CombatActor
{
    Hero,
    Enemy
}

public enum ItemRarity
{
    Common,
    Uncommon,
    Rare,
    Epic,
    Legendary,
    Mythic,
    Divine,
    Transcendent
}

public sealed class GameCalendar
{
    public int TicksPerYear { get; }
    public long TotalTicks { get; private set; }
    public int CurrentYear => checked((int)(TotalTicks / TicksPerYear) + 1);
    public int TickInYear => (int)(TotalTicks % TicksPerYear);

    public GameCalendar(int ticksPerYear)
    {
        if (ticksPerYear <= 0)
            throw new ArgumentOutOfRangeException(nameof(ticksPerYear));
        TicksPerYear = ticksPerYear;
    }

    public bool AdvanceTick()
    {
        TotalTicks = checked(TotalTicks + 1);
        return TotalTicks % TicksPerYear == 0;
    }

    public void Restore(long totalTicks)
    {
        if (totalTicks < 0)
            throw new ArgumentOutOfRangeException(nameof(totalTicks));
        TotalTicks = totalTicks;
    }
}

public sealed class CharacterAge
{
    public decimal TotalYears { get; private set; }

    public CharacterAge(decimal initialYears = 0m) => Restore(initialYears);

    public void Advance(decimal agingMultiplier, int ticksPerYear)
    {
        if (agingMultiplier < 0m)
            throw new ArgumentOutOfRangeException(nameof(agingMultiplier));
        if (ticksPerYear <= 0)
            throw new ArgumentOutOfRangeException(nameof(ticksPerYear));
        TotalYears += agingMultiplier / ticksPerYear;
    }

    public void Restore(decimal totalYears)
    {
        if (totalYears < 0m)
            throw new ArgumentOutOfRangeException(nameof(totalYears));
        TotalYears = totalYears;
    }
}

public sealed class CultivationProgress
{
    public int StageIndex { get; private set; }
    public int Level { get; private set; } = 1;
    public bool CanAttemptBreakthrough => Level == 10;

    public void IncreaseLevel()
    {
        if (Level >= 10)
            throw new InvalidOperationException("Breakthrough is required.");
        Level++;
    }

    public void BreakthroughSucceeded(int stageCount)
    {
        if (!CanAttemptBreakthrough || StageIndex >= stageCount - 1)
            throw new InvalidOperationException("Breakthrough is not available.");
        StageIndex++;
        Level = 1;
    }

    public void BreakthroughFailed(int fallbackLevel)
    {
        if (!CanAttemptBreakthrough)
            throw new InvalidOperationException("Character is not ready.");
        Level = Math.Clamp(fallbackLevel, 1, 9);
    }

    public void Restore(int stageIndex, int level, int stageCount)
    {
        if (stageIndex < 0 || stageIndex >= stageCount)
            throw new ArgumentOutOfRangeException(nameof(stageIndex));
        if (level is < 1 or > 10)
            throw new ArgumentOutOfRangeException(nameof(level));
        StageIndex = stageIndex;
        Level = level;
    }
}

public sealed class CharacterState
{
    public decimal SpiritualPower { get; private set; }
    public long Money { get; private set; }
    public CharacterAge Age { get; } = new(16m);
    public CultivationProgress Cultivation { get; } = new();
    public decimal MaximumHealthOffset { get; private set; }
    public decimal MaximumAgeOffsetYears { get; private set; }
    public decimal MaximumHealth { get; private set; } = 100m;
    public decimal Health { get; private set; } = 100m;
    public decimal Contamination { get; private set; }

    public void AddSpiritualPower(decimal amount)
    {
        if (amount < 0m)
            throw new ArgumentOutOfRangeException(nameof(amount));
        SpiritualPower += amount;
    }

    public bool TrySpendSpiritualPower(decimal amount)
    {
        if (amount < 0m)
            throw new ArgumentOutOfRangeException(nameof(amount));
        if (SpiritualPower < amount)
            return false;
        SpiritualPower -= amount;
        return true;
    }

    public void ClearSpiritualPower() => SpiritualPower = 0m;

    public bool TrySpendMoney(long amount)
    {
        if (amount < 0)
            throw new ArgumentOutOfRangeException(nameof(amount));
        if (Money < amount)
            return false;
        Money -= amount;
        return true;
    }

    public void AddMoney(long amount) => Money = checked(Money + amount);

    public void AddContamination(decimal amount)
    {
        if (amount < 0m)
            throw new ArgumentOutOfRangeException(nameof(amount));
        Contamination = Math.Clamp(Contamination + amount, 0m, 1m);
    }

    public void RestoreContamination(decimal contamination) => Contamination = Math.Clamp(contamination, 0m, 1m);
    public void ClearContamination() => Contamination = 0m;
    public void RemoveContamination(decimal amount) => Contamination = Math.Max(0m, Contamination - Math.Max(0m, amount));

    public void RestoreCheatOffsets(decimal maximumHealthOffset, decimal maximumAgeOffsetYears)
    {
        MaximumHealthOffset = maximumHealthOffset;
        MaximumAgeOffsetYears = maximumAgeOffsetYears;
    }

    public void AdjustMaximumHealthOffset(decimal amount) => MaximumHealthOffset += amount;

    public void AdjustMaximumAgeOffset(decimal years) => MaximumAgeOffsetYears += years;

    public void ConfigureMaximumHealth(decimal maximumHealth, bool fillIfUninitialized = false)
    {
        if (maximumHealth <= 0m)
            throw new ArgumentOutOfRangeException(nameof(maximumHealth));
        var wasFull = Health >= MaximumHealth;
        MaximumHealth = maximumHealth;
        Health = fillIfUninitialized || wasFull
            ? maximumHealth
            : Math.Clamp(Health, 0m, maximumHealth);
    }

    public decimal TakeDamage(decimal amount)
    {
        if (amount < 0m)
            throw new ArgumentOutOfRangeException(nameof(amount));
        var applied = Math.Min(Health, amount);
        Health -= applied;
        return applied;
    }

    public void Heal(decimal amount)
    {
        if (amount < 0m)
            throw new ArgumentOutOfRangeException(nameof(amount));
        Health = Math.Min(MaximumHealth, Health + amount);
    }

    public void RestoreHealth(decimal health, decimal maximumHealth)
    {
        if (maximumHealth <= 0m)
            throw new InvalidDataException("Maximum health must be positive.");
        MaximumHealth = maximumHealth;
        Health = Math.Clamp(health, 0m, maximumHealth);
    }

    public void Restore(decimal spiritualPower, long money, decimal totalYears)
    {
        if (spiritualPower < 0m || money < 0)
            throw new InvalidDataException("Character resources cannot be negative.");
        SpiritualPower = spiritualPower;
        Money = money;
        Age.Restore(totalYears);
    }
}

public sealed class MissionEncounter
{
    public required string MonsterConfigId { get; init; }
    public required string BackgroundId { get; init; }
    public int DangerLevel { get; init; }
    public decimal TriggerProgress { get; init; }
    public bool Resolved { get; private set; }

    public void MarkResolved() => Resolved = true;
    public void RestoreResolved(bool resolved) => Resolved = resolved;
}

public sealed class ActiveCombat
{
    public required string MonsterConfigId { get; init; }
    public required string BackgroundId { get; init; }
    public int DangerLevel { get; init; }
    public decimal EnemyMaximumHealth { get; init; }
    public decimal EnemyHealth { get; private set; }
    public float HeroCooldown { get; private set; }
    public float EnemyCooldown { get; private set; }
    public float FinishDelay { get; private set; }
    public CombatPhase Phase { get; private set; } = CombatPhase.Fighting;

    public bool IsFinished => Phase != CombatPhase.Fighting;

    public void Initialize(decimal enemyHealth, float heroCooldown, float enemyCooldown)
    {
        EnemyHealth = Math.Clamp(enemyHealth, 0m, EnemyMaximumHealth);
        HeroCooldown = Math.Max(0f, heroCooldown);
        EnemyCooldown = Math.Max(0f, enemyCooldown);
    }

    public void AdvanceCooldowns(float deltaTime)
    {
        HeroCooldown -= deltaTime;
        EnemyCooldown -= deltaTime;
    }

    public void ResetCooldown(CombatActor actor, float seconds)
    {
        if (actor == CombatActor.Hero)
            HeroCooldown += Math.Max(0.05f, seconds);
        else
            EnemyCooldown += Math.Max(0.05f, seconds);
    }

    public decimal DamageEnemy(decimal amount)
    {
        var applied = Math.Min(EnemyHealth, Math.Max(0m, amount));
        EnemyHealth -= applied;
        return applied;
    }

    public void Finish(CombatPhase phase, float delay)
    {
        if (phase == CombatPhase.Fighting)
            throw new ArgumentOutOfRangeException(nameof(phase));
        Phase = phase;
        FinishDelay = Math.Max(0f, delay);
    }

    public bool AdvanceFinishDelay(float deltaTime)
    {
        FinishDelay = Math.Max(0f, FinishDelay - deltaTime);
        return FinishDelay <= 0f;
    }

    public void Restore(
        decimal enemyHealth,
        float heroCooldown,
        float enemyCooldown,
        CombatPhase phase,
        float finishDelay)
    {
        Initialize(enemyHealth, heroCooldown, enemyCooldown);
        Phase = phase;
        FinishDelay = Math.Max(0f, finishDelay);
    }
}

public sealed record ItemEffectDefinition
{
    public EffectType Type { get; init; }
    public ModifierOperation Operation { get; init; }
    public decimal Value { get; init; }
}

public sealed record AlchemyPropertyAmount
{
    public string PropertyId { get; init; } = string.Empty;
}

public sealed class ActiveEffect
{
    public string SourceItemId { get; init; } = string.Empty;
    public EffectType Type { get; init; }
    public ModifierOperation Operation { get; init; }
    public decimal Value { get; init; }
    public ItemRarity SourceRarity { get; init; }
    public decimal SourceQuality { get; init; } = 2.5m;
    public int? RemainingTicks { get; private set; }
    public ItemDurationType DurationType { get; init; } = ItemDurationType.Temporary;
    public bool IsPermanent => DurationType == ItemDurationType.Permanent;
    public bool IsUntilBreakthroughAttempt => DurationType == ItemDurationType.UntilBreakthroughAttempt;
    public bool IsExpired => RemainingTicks is <= 0;

    public ActiveEffect()
    {
    }

    public ActiveEffect(
        string sourceItemId,
        ItemEffectDefinition definition,
        decimal scaledValue,
        int? remainingTicks,
        ItemDurationType durationType,
        ItemRarity sourceRarity,
        decimal sourceQuality)
    {
        SourceItemId = sourceItemId;
        Type = definition.Type;
        Operation = definition.Operation;
        Value = scaledValue;
        RemainingTicks = remainingTicks;
        DurationType = durationType;
        SourceRarity = sourceRarity;
        SourceQuality = sourceQuality;
    }

    public void AdvanceTick()
    {
        if (DurationType == ItemDurationType.Temporary && RemainingTicks is not null)
            RemainingTicks--;
    }

    public void RestoreDuration(int? remainingTicks) => RemainingTicks = remainingTicks;
}

public sealed record MissionItemRewardRoll
{
    public ItemRarity Rarity { get; init; }
    public decimal Quality { get; init; }
    public decimal Contamination { get; init; }
}

public sealed record MissionReward
{
    public MissionRewardType Type { get; init; }
    public long Money { get; init; }
    public string? ItemConfigId { get; init; }
    public ItemRarity ItemRarity { get; init; }
    public decimal ItemQuality { get; init; }
    public int Quantity { get; init; } = 1;
    public List<MissionItemRewardRoll> ItemRolls { get; init; } = [];
}

public sealed class ItemInstance
{
    public required Guid InstanceId { get; init; }
    public required string ConfigId { get; init; }
    public ItemRarity Rarity { get; init; }
    public decimal Quality { get; init; }
    public decimal Contamination { get; init; }
    public decimal PurificationPercent { get; init; }
    public string? CustomName { get; init; }
    public string? CustomDescription { get; init; }
    public string? AlchemyOriginId { get; init; }
    public int DistillationLevel { get; init; }
    public int? CraftedDurationTicks { get; init; }
    public List<ItemEffectDefinition> CraftedEffects { get; init; } = [];
    public List<AlchemyPropertyAmount> AlchemyProperties { get; init; } = [];
    public int Quantity { get; private set; } = 1;

    public void AddQuantity(int amount)
    {
        if (amount <= 0)
            throw new ArgumentOutOfRangeException(nameof(amount));
        Quantity = checked(Quantity + amount);
    }

    public void RemoveQuantity(int amount)
    {
        if (amount <= 0 || amount > Quantity)
            throw new ArgumentOutOfRangeException(nameof(amount));
        Quantity -= amount;
    }

    public void RestoreQuantity(int quantity) =>
        Quantity = quantity > 0 ? quantity : throw new ArgumentOutOfRangeException(nameof(quantity));

    public ItemInstance Copy(int quantity = 1)
    {
        if (quantity <= 0)
            throw new ArgumentOutOfRangeException(nameof(quantity));
        var copy = new ItemInstance
        {
            InstanceId = Guid.NewGuid(),
            ConfigId = ConfigId,
            Rarity = Rarity,
            Quality = Quality,
            Contamination = Contamination,
            PurificationPercent = PurificationPercent,
            CustomName = CustomName,
            CustomDescription = CustomDescription,
            AlchemyOriginId = AlchemyOriginId,
            DistillationLevel = DistillationLevel,
            CraftedDurationTicks = CraftedDurationTicks,
            CraftedEffects = [.. CraftedEffects],
            AlchemyProperties = [.. AlchemyProperties]
        };
        if (quantity > 1)
            copy.AddQuantity(quantity - 1);
        return copy;
    }

    public bool CanStackWith(ItemInstance other) =>
        ConfigId == other.ConfigId &&
        Rarity == other.Rarity &&
        Quality == other.Quality &&
        Contamination == other.Contamination &&
        PurificationPercent == other.PurificationPercent &&
        CustomName == other.CustomName &&
        CustomDescription == other.CustomDescription &&
        AlchemyOriginId == other.AlchemyOriginId &&
        DistillationLevel == other.DistillationLevel &&
        CraftedDurationTicks == other.CraftedDurationTicks &&
        CraftedEffects.SequenceEqual(other.CraftedEffects) &&
        AlchemyProperties.SequenceEqual(other.AlchemyProperties);
}

public sealed class Inventory
{
    private readonly List<ItemInstance> _items = [];
    public IReadOnlyList<ItemInstance> Items => _items;

    public void Add(ItemInstance item)
    {
        ArgumentNullException.ThrowIfNull(item);
        var stack = _items.FirstOrDefault(candidate => candidate.CanStackWith(item));
        if (stack is null)
            _items.Add(item);
        else
            stack.AddQuantity(item.Quantity);
    }

    public bool Remove(Guid instanceId, int quantity)
    {
        var item = _items.FirstOrDefault(candidate => candidate.InstanceId == instanceId);
        if (item is null || quantity <= 0 || item.Quantity < quantity)
            return false;
        item.RemoveQuantity(quantity);
        if (item.Quantity == 0)
            _items.Remove(item);
        return true;
    }

    public ItemInstance? Find(Guid instanceId) =>
        _items.FirstOrDefault(item => item.InstanceId == instanceId);

    public void ReplaceWith(IEnumerable<ItemInstance> items)
    {
        _items.Clear();
        foreach (var item in items)
            Add(item);
    }
}

public sealed class ActiveMission
{
    public Guid InstanceId { get; init; } = Guid.NewGuid();
    public required string MissionConfigId { get; init; }
    public int? DangerLevel { get; init; }
    public decimal RequiredProgress { get; init; }
    public decimal CurrentProgress { get; private set; }
    public bool RewardGranted { get; private set; }
    public bool IsCompleted => CurrentProgress >= RequiredProgress;
    public List<MissionReward> Rewards { get; init; } = [];
    public MissionEncounter? Encounter { get; init; }
    public ActiveCombat? Combat { get; private set; }
    public bool IsInCombat => Combat is not null;

    public void AddProgress(decimal amount)
    {
        if (amount < 0m)
            throw new ArgumentOutOfRangeException(nameof(amount));
        var maximum = Encounter is { Resolved: false } ? Encounter.TriggerProgress : RequiredProgress;
        CurrentProgress = Math.Min(maximum, CurrentProgress + amount);
    }

    public void MarkRewardGranted()
    {
        if (!IsCompleted)
            throw new InvalidOperationException("Mission is not completed.");
        RewardGranted = true;
    }

    public void StartCombat(ActiveCombat combat)
    {
        if (Encounter is null || Encounter.Resolved || Combat is not null)
            throw new InvalidOperationException("Mission encounter cannot start.");
        Combat = combat;
    }

    public void ResolveCombat()
    {
        Encounter?.MarkResolved();
        Combat = null;
    }

    public void RestoreCombat(ActiveCombat? combat) => Combat = combat;

    public void Restore(decimal progress, bool rewardGranted)
    {
        CurrentProgress = Math.Clamp(progress, 0m, RequiredProgress);
        RewardGranted = rewardGranted;
    }
}

public sealed class ShopSlot
{
    public Guid SlotId { get; init; }
    public ItemInstance Item { get; init; } = null!;
    public int AvailableQuantity { get; private set; }

    public ShopSlot()
    {
    }

    public ShopSlot(ItemInstance item, int quantity)
    {
        SlotId = Guid.NewGuid();
        Item = item;
        AvailableQuantity = quantity > 0 ? quantity : throw new ArgumentOutOfRangeException(nameof(quantity));
    }

    public void Remove(int amount)
    {
        if (amount <= 0 || amount > AvailableQuantity)
            throw new ArgumentOutOfRangeException(nameof(amount));
        AvailableQuantity -= amount;
    }

    public void RestoreQuantity(int quantity) =>
        AvailableQuantity = quantity >= 0 ? quantity : throw new ArgumentOutOfRangeException(nameof(quantity));
}

public sealed class ShopState
{
    private readonly List<ShopSlot> _slots = [];
    public int BuyMarkupPercent { get; private set; }
    public int SellAdjustmentPercent { get; private set; }
    public IReadOnlyList<ShopSlot> Slots => _slots;

    public void ReplaceStock(IEnumerable<ShopSlot> slots, int buyMarkup, int sellAdjustment)
    {
        _slots.Clear();
        _slots.AddRange(slots);
        BuyMarkupPercent = buyMarkup;
        SellAdjustmentPercent = sellAdjustment;
    }
}

public sealed class MissionBoardState
{
    private readonly List<MissionOffer> _offers = [];

    public IReadOnlyList<MissionOffer> Offers => _offers;
    public IReadOnlyList<string> MissionIds => _offers.Select(offer => offer.MissionConfigId).ToList();

    public MissionOffer? Find(Guid offerId) => _offers.FirstOrDefault(offer => offer.OfferId == offerId);

    public MissionOffer? FindByMissionId(string missionId) =>
        _offers.FirstOrDefault(offer => offer.MissionConfigId == missionId);

    public bool Take(Guid offerId)
    {
        var offer = Find(offerId);
        return offer is not null && _offers.Remove(offer);
    }

    public void ReplaceWith(IEnumerable<MissionOffer> offers)
    {
        ArgumentNullException.ThrowIfNull(offers);
        _offers.Clear();
        _offers.AddRange(offers);
    }

    public void ReplaceWithLegacy(IEnumerable<string> missionIds) =>
        ReplaceWith(missionIds.Select(missionId => new MissionOffer { MissionConfigId = missionId }));
}

public sealed class MissionOffer
{
    public Guid OfferId { get; init; } = Guid.NewGuid();
    public required string MissionConfigId { get; init; }
    public int? DangerLevel { get; init; }
}

public sealed class GameState
{
    private readonly List<ActiveMission> _missionQueue = [];

    public GameCalendar Calendar { get; }
    public CharacterState Character { get; } = new();
    public Inventory Inventory { get; } = new();
    public ShopState Shop { get; } = new();
    public MissionBoardState MissionBoard { get; } = new();
    public GameSettings Settings { get; } = new();
    public IReadOnlyList<ActiveMission> MissionQueue => _missionQueue;
    public ActiveMission? CurrentMission => _missionQueue.FirstOrDefault();
    public List<ActiveEffect> ActiveEffects { get; } = [];
    public ActivityMode ActivityMode { get; private set; } = ActivityMode.Cultivation;
    public bool RecoveryRequired { get; private set; }

    public GameState(int ticksPerYear) => Calendar = new GameCalendar(ticksPerYear);

    public void SetActivityMode(ActivityMode mode)
    {
        ActivityMode = mode;
    }

    public void BeginDefeatRecovery()
    {
        RecoveryRequired = false;
    }

    public void CompleteDefeatRecovery() => RecoveryRequired = false;

    public void RestoreDefeatRecovery(bool required)
    {
        RecoveryRequired = false;
    }

    public void EnqueueMission(ActiveMission mission)
    {
        ArgumentNullException.ThrowIfNull(mission);
        _missionQueue.Add(mission);
    }

    public bool RemoveMission(Guid instanceId)
    {
        var mission = _missionQueue.FirstOrDefault(candidate => candidate.InstanceId == instanceId);
        return mission is not null && _missionQueue.Remove(mission);
    }

    public bool MoveMission(Guid instanceId, int offset)
    {
        if (offset is not (-1 or 1))
            throw new ArgumentOutOfRangeException(nameof(offset));
        var currentIndex = _missionQueue.FindIndex(mission => mission.InstanceId == instanceId);
        var targetIndex = currentIndex + offset;
        if (currentIndex < 0 || targetIndex < 0 || targetIndex >= _missionQueue.Count)
            return false;
        (_missionQueue[currentIndex], _missionQueue[targetIndex]) =
            (_missionQueue[targetIndex], _missionQueue[currentIndex]);
        return true;
    }
}

public sealed class GameSettings
{
    public bool MusicEnabled { get; private set; } = true;
    public bool SoundsEnabled { get; private set; } = true;
    public bool PrivacyPolicyAccepted { get; private set; }

    public void Restore(bool musicEnabled, bool soundsEnabled, bool privacyPolicyAccepted = false)
    {
        MusicEnabled = musicEnabled;
        SoundsEnabled = soundsEnabled;
        PrivacyPolicyAccepted = privacyPolicyAccepted;
    }

    public void ToggleMusic() => MusicEnabled = !MusicEnabled;
    public void ToggleSounds() => SoundsEnabled = !SoundsEnabled;
    public void AcceptPrivacyPolicy() => PrivacyPolicyAccepted = true;
}

public readonly record struct TickModifiers(
    decimal TickEfficiency,
    decimal AgingMultiplier,
    decimal TimeAccelerationMultiplier,
    decimal SpiritualPowerMultiplier,
    decimal MissionProgressMultiplier,
    decimal BreakthroughChanceBonus);

public sealed record TickResult(
    long TickNumber,
    int Year,
    decimal SpiritualPowerGained,
    decimal MissionProgressAdded,
    bool MissionCompleted,
    int LevelsGained,
    bool NewYearStarted,
    bool CharacterDied);

public sealed record TapResult(
    decimal SpiritualPowerGained,
    int LevelsGained);

public sealed record BreakthroughResult(
    bool Success,
    decimal FinalChance,
    int StageIndex,
    int Level,
    int LevelsLost,
    string Message);

public sealed record TransactionResult(bool Success, string Message, long TotalPrice = 0)
{
    public static TransactionResult Fail(string message) => new(false, message);
    public static TransactionResult Ok(long price, string message) => new(true, message, price);
}
