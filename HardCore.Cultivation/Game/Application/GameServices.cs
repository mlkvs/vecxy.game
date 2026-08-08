using HardCore.Cultivation.Game.Domain;
using HardCore.Cultivation.Game.Infrastructure;
using GameState = HardCore.Cultivation.Game.Domain.GameState;

namespace HardCore.Cultivation.Game.Application;

public static class WeightedRandom
{
    public static T Select<T>(
        IReadOnlyList<T> values,
        Func<T, decimal> weightSelector,
        IRandomSource random)
    {
        if (values.Count == 0)
            throw new ArgumentException("Collection is empty.", nameof(values));
        var totalWeight = values.Sum(weightSelector);
        if (totalWeight <= 0m)
            throw new InvalidOperationException("Total weight must be positive.");
        var roll = random.NextDecimal(0m, totalWeight);
        var current = 0m;
        foreach (var value in values)
        {
            current += weightSelector(value);
            if (roll < current)
                return value;
        }
        return values[^1];
    }
}

public static class ModifierCalculator
{
    public static decimal Calculate(
        decimal baseValue,
        IEnumerable<ActiveEffect> effects,
        EffectType targetType)
    {
        var flat = 0m;
        var additivePercent = 0m;
        var multiplicative = 1m;
        foreach (var effect in effects.Where(effect => effect.Type == targetType))
        {
            switch (effect.Operation)
            {
                case ModifierOperation.Flat:
                    flat += effect.Value;
                    break;
                case ModifierOperation.AdditivePercent:
                    additivePercent += effect.Value / 100m;
                    break;
                case ModifierOperation.MultiplicativePercent:
                    multiplicative *= 1m + effect.Value / 100m;
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }
        return (baseValue + flat) * (1m + additivePercent) * multiplicative;
    }
}

public sealed class ItemGenerator(GameDatabase database, IRandomSource random)
{
    public ItemInstance Generate(string configId, int quantity = 1)
    {
        _ = database.GetItem(configId);
        var rarity = WeightedRandom.Select(
            database.Rarities.Values.ToArray(),
            config => config.ShopWeight,
            random).Rarity;
        var band = WeightedRandom.Select(
            database.Balance.QualityBands,
            qualityBand => qualityBand.Weight,
            random);
        var step = random.NextInt(1, 11);
        var item = new ItemInstance
        {
            InstanceId = Guid.NewGuid(),
            ConfigId = configId,
            Rarity = rarity,
            Quality = band.Index - 1 + step / 10m
        };
        if (quantity > 1)
            item.AddQuantity(quantity - 1);
        return item;
    }
}

public sealed class ItemEffectService(GameDatabase database)
{
    public TickModifiers CalculateModifiers(GameState state)
    {
        var effects = state.ActiveEffects.Where(effect => !effect.IsExpired).ToArray();
        var tickEfficiency = Math.Max(
            database.Balance.MinimumTickEfficiency,
            ModifierCalculator.Calculate(1m, effects, EffectType.TickEfficiency));
        var aging = Math.Max(
            database.Balance.MinimumAgingMultiplier,
            ModifierCalculator.Calculate(1m, effects, EffectType.AgingSpeed));
        var spiritual = Math.Max(0m,
            ModifierCalculator.Calculate(1m, effects, EffectType.SpiritualPowerGain));
        var mission = Math.Max(0m,
            ModifierCalculator.Calculate(1m, effects, EffectType.MissionProgress));
        var breakthrough = ModifierCalculator.Calculate(0m, effects, EffectType.BreakthroughChance);
        return new TickModifiers(tickEfficiency, aging, spiritual, mission, breakthrough);
    }

    public TransactionResult Use(GameState state, Guid instanceId)
    {
        var item = state.Inventory.Find(instanceId);
        if (item is null)
            return TransactionResult.Fail("Предмет не найден.");
        var config = database.GetItem(item.ConfigId);
        var strength = database.Balance.EffectQualityBase +
                       item.Quality * database.Balance.EffectQualityPerPoint;

        if (config.DurationType == ItemDurationType.Instant)
        {
            foreach (var effect in config.Effects)
            {
                var value = effect.Value * strength;
                if (effect.Type == EffectType.SpiritualPowerGain)
                    state.Character.AddSpiritualPower(Math.Max(0m, value));
                else
                    return TransactionResult.Fail("Этот мгновенный эффект пока не поддерживается.");
            }
        }
        else
        {
            int? duration = config.DurationType == ItemDurationType.Temporary
                ? config.TemporaryDurationTicks
                : null;
            foreach (var definition in config.Effects)
            {
                state.ActiveEffects.Add(new ActiveEffect(
                    config.Id,
                    definition,
                    definition.Value * strength,
                    duration,
                    config.DurationType,
                    item.Rarity,
                    item.Quality));
            }
        }

        state.Inventory.Remove(item.InstanceId, 1);
        return TransactionResult.Ok(0, $"Использовано: {config.Name}");
    }

    public void AdvanceTemporaryEffects(GameState state)
    {
        foreach (var effect in state.ActiveEffects)
            effect.AdvanceTick();
        state.ActiveEffects.RemoveAll(effect => effect.IsExpired);
    }

    public void ConsumeBreakthroughAttemptEffects(GameState state) =>
        state.ActiveEffects.RemoveAll(effect => effect.IsUntilBreakthroughAttempt);
}

public static class QualityPriceCurve
{
    public static decimal Evaluate(decimal quality, IReadOnlyList<PriceCurvePoint> points)
    {
        if (points.Count == 0)
            throw new ArgumentException("Price curve is empty.", nameof(points));
        var ordered = points.OrderBy(point => point.Quality).ToArray();
        if (quality <= ordered[0].Quality)
            return ordered[0].Multiplier;
        if (quality >= ordered[^1].Quality)
            return ordered[^1].Multiplier;
        for (var index = 0; index < ordered.Length - 1; index++)
        {
            var left = ordered[index];
            var right = ordered[index + 1];
            if (quality < left.Quality || quality > right.Quality)
                continue;
            var position = (quality - left.Quality) / (right.Quality - left.Quality);
            return left.Multiplier + (right.Multiplier - left.Multiplier) * position;
        }
        throw new InvalidOperationException("Unable to evaluate price curve.");
    }
}

public sealed class ItemPriceCalculator(GameDatabase database)
{
    public long GetValue(ItemInstance item)
    {
        var config = database.GetItem(item.ConfigId);
        var quality = QualityPriceCurve.Evaluate(item.Quality, database.Balance.QualityPriceCurve);
        var rarity = database.GetRarity(item.Rarity).PriceMultiplier;
        return Round(config.BasePrice * quality * rarity);
    }

    public long GetBuyPrice(ItemInstance item, ShopState shop) =>
        Round(GetValue(item) * (1m + shop.BuyMarkupPercent / 100m));

    public long GetSellPrice(ItemInstance item, ShopState shop) =>
        Math.Max(1, Round(GetValue(item) * (1m + shop.SellAdjustmentPercent / 100m)));

    private static long Round(decimal value) => checked((long)decimal.Round(
        value,
        0,
        MidpointRounding.AwayFromZero));
}

public sealed class MissionService(
    GameDatabase database,
    ItemGenerator itemGenerator,
    IRandomSource random)
{
    public void Refresh(GameState state)
    {
        var candidates = database.Missions.Values.ToList();
        var offers = new List<string>(database.MissionBoardSlotCount);
        while (offers.Count < database.MissionBoardSlotCount && candidates.Count > 0)
        {
            var selected = WeightedRandom.Select(candidates, mission => mission.BoardWeight, random);
            offers.Add(selected.Id);
            candidates.Remove(selected);
        }
        state.MissionBoard.ReplaceWith(offers);
    }

    public TransactionResult Start(GameState state, string missionId)
    {
        if (state.MissionQueue.Count >= database.Balance.MaximumMissionQueueSize)
            return TransactionResult.Fail("Очередь миссий заполнена.");
        if (!state.MissionBoard.Contains(missionId))
            return TransactionResult.Fail("Это поручение уже недоступно.");
        var config = database.GetMission(missionId);
        var required = random.NextInt(config.MinimumDurationTicks, config.MaximumDurationTicks + 1);
        var encounter = RollEncounter(config, required);
        state.EnqueueMission(new ActiveMission
        {
            MissionConfigId = missionId,
            RequiredProgress = required,
            Rewards = GenerateRewards(config),
            Encounter = encounter
        });
        state.MissionBoard.Take(missionId);
        return TransactionResult.Ok(0, $"Добавлено в очередь: {config.Name}");
    }

    private MissionEncounter? RollEncounter(MissionConfig mission, decimal requiredProgress)
    {
        if (mission.DangerLevel is not { } level)
            return null;
        var danger = database.GetDanger(level);
        if (random.NextDecimal(0m, 100m) >= danger.EncounterChancePercent)
            return null;
        var monsters = mission.PossibleMonsterIds.Select(database.GetMonster).ToArray();
        var monster = WeightedRandom.Select(monsters, value => value.SelectionWeight, random);
        var background = mission.PossibleBackgroundIds[random.NextInt(0, mission.PossibleBackgroundIds.Count)];
        return new MissionEncounter
        {
            MonsterConfigId = monster.Id,
            BackgroundId = background,
            DangerLevel = level,
            TriggerProgress = decimal.Round(requiredProgress * random.NextDecimal(0.25m, 0.75m), 2)
        };
    }

    public TransactionResult Remove(GameState state, Guid missionInstanceId)
    {
        var mission = state.MissionQueue.FirstOrDefault(value => value.InstanceId == missionInstanceId);
        if (mission?.IsInCombat == true)
            return TransactionResult.Fail("Нельзя покинуть поручение во время боя.");
        return state.RemoveMission(missionInstanceId)
            ? TransactionResult.Ok(0, "Миссия удалена из очереди.")
            : TransactionResult.Fail("Миссия не найдена.");
    }

    public TransactionResult Move(GameState state, Guid missionInstanceId, int offset)
    {
        if (state.CurrentMission?.IsInCombat == true)
            return TransactionResult.Fail("Нельзя менять очередь во время боя.");
        return state.MoveMission(missionInstanceId, offset)
            ? TransactionResult.Ok(0, "Порядок миссий изменён.")
            : TransactionResult.Fail("Миссию нельзя переместить дальше.");
    }

    public bool AdvanceCurrentMission(GameState state, decimal progress)
    {
        var mission = state.CurrentMission;
        if (mission is null || mission.IsCompleted)
            return false;
        mission.AddProgress(progress);
        if (!mission.IsCompleted || mission.RewardGranted)
            return false;

        var config = database.GetMission(mission.MissionConfigId);
        foreach (var reward in mission.Rewards)
        {
            if (reward.Type == MissionRewardType.Money)
            {
                state.Character.AddMoney(reward.Money);
                continue;
            }
            var item = new ItemInstance
            {
                InstanceId = Guid.NewGuid(),
                ConfigId = reward.ItemConfigId!,
                Rarity = reward.ItemRarity,
                Quality = reward.ItemQuality
            };
            if (reward.Quantity > 1)
                item.AddQuantity(reward.Quantity - 1);
            state.Inventory.Add(item);
        }
        mission.MarkRewardGranted();
        state.RemoveMission(mission.InstanceId);
        return true;
    }

    private List<MissionReward> GenerateRewards(MissionConfig mission)
    {
        var result = new List<MissionReward>(2);
        var candidates = database.Items.Values
            .Where(item => mission.Reward.RequiredItemCategory is null ||
                           item.Category == mission.Reward.RequiredItemCategory)
            .ToArray();
        var canGiveMoney = mission.Reward.Money > 0;
        var canGiveItem = candidates.Length > 0;

        if (!canGiveMoney && !canGiveItem)
            throw new InvalidOperationException($"Mission reward pool is empty: {mission.Id}");

        var rewardCount = random.NextInt(1, 3);
        var includeMoney = canGiveMoney && (!canGiveItem || random.NextInt(0, 2) == 0);

        if (includeMoney)
        {
            result.Add(new MissionReward
            {
                Type = MissionRewardType.Money,
                Money = mission.Reward.Money
            });
        }

        while (result.Count < rewardCount && canGiveItem)
        {
            var config = WeightedRandom.Select(candidates, item => item.ShopWeight, random);
            var generated = itemGenerator.Generate(config.Id);
            result.Add(new MissionReward
            {
                Type = MissionRewardType.Item,
                ItemConfigId = config.Id,
                ItemRarity = generated.Rarity,
                ItemQuality = generated.Quality,
                Quantity = random.NextInt(mission.Reward.MinimumQuantity, mission.Reward.MaximumQuantity + 1)
            });
        }

        if (result.Count == 0 && canGiveItem)
        {
            var config = WeightedRandom.Select(candidates, item => item.ShopWeight, random);
            var generated = itemGenerator.Generate(config.Id);
            result.Add(new MissionReward
            {
                Type = MissionRewardType.Item,
                ItemConfigId = config.Id,
                ItemRarity = generated.Rarity,
                ItemQuality = generated.Quality,
                Quantity = random.NextInt(mission.Reward.MinimumQuantity, mission.Reward.MaximumQuantity + 1)
            });
        }

        return result;
    }
}

public sealed class ShopService(
    GameDatabase database,
    ItemGenerator itemGenerator,
    IRandomSource random)
{
    public void Refresh(ShopState shop)
    {
        var slots = new List<ShopSlot>();
        var items = database.Items.Values.ToArray();
        for (var index = 0; index < database.Shop.SlotCount; index++)
        {
            var config = WeightedRandom.Select(items, item => item.ShopWeight, random);
            var quantity = random.NextInt(
                database.Shop.MinimumQuantity,
                database.Shop.MaximumQuantity + 1);
            slots.Add(new ShopSlot(itemGenerator.Generate(config.Id), quantity));
        }
        shop.ReplaceStock(
            slots,
            random.NextInt(database.Shop.MinimumBuyMarkup, database.Shop.MaximumBuyMarkup + 1),
            database.Shop.SellAdjustmentPercent);
    }
}

public sealed class ShopTransactionService(ItemPriceCalculator prices)
{
    public TransactionResult Buy(GameState state, Guid slotId, int quantity = 1)
    {
        if (quantity <= 0)
            return TransactionResult.Fail("Некорректное количество.");
        var slot = state.Shop.Slots.FirstOrDefault(candidate => candidate.SlotId == slotId);
        if (slot is null || slot.AvailableQuantity < quantity)
            return TransactionResult.Fail("Товар закончился.");
        long total;
        try
        {
            total = checked(prices.GetBuyPrice(slot.Item, state.Shop) * quantity);
        }
        catch (OverflowException)
        {
            return TransactionResult.Fail("Цена слишком велика.");
        }
        if (!state.Character.TrySpendMoney(total))
            return TransactionResult.Fail("Недостаточно рублей.");
        slot.Remove(quantity);
        state.Inventory.Add(slot.Item.Copy(quantity));
        return TransactionResult.Ok(total, "Покупка завершена.");
    }

    public TransactionResult Sell(GameState state, Guid instanceId, int quantity = 1)
    {
        var item = state.Inventory.Find(instanceId);
        if (item is null || quantity <= 0 || item.Quantity < quantity)
            return TransactionResult.Fail("Предмет не найден.");
        long total;
        try
        {
            total = checked(prices.GetSellPrice(item, state.Shop) * quantity);
            state.Character.AddMoney(total);
        }
        catch (OverflowException)
        {
            return TransactionResult.Fail("Сумма слишком велика.");
        }
        state.Inventory.Remove(instanceId, quantity);
        return TransactionResult.Ok(total, "Предмет продан.");
    }
}

public sealed class CultivationService(GameDatabase database, IRandomSource random)
{
    private static readonly decimal[] LongevityStageBonuses = [20m, 40m, 80m, 150m, 250m, 400m, 600m, 1000m];

    public decimal GetRequiredPower(int stageIndex, int level)
    {
        if (level is < 1 or > 10)
            throw new ArgumentOutOfRangeException(nameof(level));
        var stage = database.Cultivation.Stages[stageIndex];
        return database.Cultivation.BaseRequiredPower *
               database.Cultivation.LevelMultipliers[level - 1] *
               stage.StageMultiplier;
    }

    public decimal GetMaximumAge(CharacterState character)
    {
        ArgumentNullException.ThrowIfNull(character);
        var unlockedStages = Math.Clamp(character.Cultivation.StageIndex, 0, LongevityStageBonuses.Length);
        var bonus = LongevityStageBonuses.Take(unlockedStages).Sum();
        return database.Balance.MaximumAgeYears + bonus;
    }

    public TransactionResult TryAdvanceLevel(CharacterState character)
    {
        if (character.Cultivation.Level >= 10)
            return TransactionResult.Fail("Для дальнейшего роста нужен прорыв.");
        var cost = GetRequiredPower(
            character.Cultivation.StageIndex,
            character.Cultivation.Level);
        if (!character.TrySpendSpiritualPower(cost))
            return TransactionResult.Fail("Недостаточно духовной силы.");
        character.Cultivation.IncreaseLevel();
        return TransactionResult.Ok(0, "Уровень культивации повышен.");
    }

    public int AdvanceLevelsAutomatically(CharacterState character)
    {
        var gained = 0;
        while (character.Cultivation.Level < 10)
        {
            var result = TryAdvanceLevel(character);
            if (!result.Success)
                break;
            gained++;
        }
        return gained;
    }

    public decimal GetBreakthroughChance(CharacterState character, IReadOnlyCollection<ActiveEffect> effects)
    {
        var progress = character.Cultivation;
        if (!progress.CanAttemptBreakthrough || progress.StageIndex >= database.Cultivation.Stages.Count - 1)
            return 0m;
        var baseChance = database.Cultivation.Stages[progress.StageIndex].BaseBreakthroughChance;
        return Math.Clamp(
            ModifierCalculator.Calculate(baseChance, effects, EffectType.BreakthroughChance),
            0m,
            database.Balance.MaximumBreakthroughChance);
    }

    public BreakthroughResult AttemptBreakthrough(
        CharacterState character,
        List<ActiveEffect> effects)
    {
        var progress = character.Cultivation;
        if (!progress.CanAttemptBreakthrough)
            return new(false, 0m, progress.StageIndex, progress.Level, 0, "Прорыв доступен только на 10 уровне.");
        if (progress.StageIndex >= database.Cultivation.Stages.Count - 1)
            return new(false, 100m, progress.StageIndex, progress.Level, 0, "Достигнута высшая ступень.");
        var cost = GetRequiredPower(progress.StageIndex, 10);
        if (!character.TrySpendSpiritualPower(cost))
            return new(false, 0m, progress.StageIndex, progress.Level, 0, "Недостаточно духовной силы.");

        var chance = GetBreakthroughChance(character, effects);
        effects.RemoveAll(effect => effect.IsUntilBreakthroughAttempt);
        if (random.NextDecimal(0m, 100m) < chance)
        {
            progress.BreakthroughSucceeded(database.Cultivation.Stages.Count);
            return new(true, chance, progress.StageIndex, progress.Level, 0, "Прорыв успешен.");
        }
        var fallback = random.NextInt(1, 10);
        progress.BreakthroughFailed(fallback);
        var lost = 10 - fallback;
        return new(false, chance, progress.StageIndex, progress.Level, lost,
            $"Прорыв не удался, вы получили травму и потеряли {lost} уровней");
    }
}

public readonly record struct DogMeditationResult(bool Success, long Reward, string Message);

public sealed class DogMeditationService(GameDatabase database, IRandomSource random)
{
    public float GetProgress(GameState state) =>
        state.DogMeditation.GetProgress(database.Dog.ChargeDurationSeconds);

    public bool Update(GameState state, float deltaTime) =>
        state.DogMeditation.Advance(deltaTime, database.Dog.ChargeDurationSeconds);

    public DogMeditationResult Collect(GameState state)
    {
        if (!state.DogMeditation.IsReady(database.Dog.ChargeDurationSeconds))
            return new(false, 0, "Собака ещё медитирует.");

        var rewardUnits = random.NextInt(
            database.Dog.MinimumRewardUnits,
            checked(database.Dog.MaximumRewardUnits + 1));
        var reward = checked((long)rewardUnits * database.Dog.RewardUnitRubles);
        state.Character.AddMoney(reward);
        state.DogMeditation.Reset();
        return new(true, reward, $"Собака намедитировала {reward:N0} рублей.");
    }
}

public enum CombatEventType
{
    Started,
    HeroAttack,
    EnemyAttack,
    HeroHurt,
    EnemyHurt,
    HeroDied,
    EnemyDied,
    Victory,
    Defeat,
    Closed
}

public readonly record struct CombatEvent(CombatEventType Type, decimal Amount = 0m);

public sealed class CombatUpdate
{
    public List<CombatEvent> Events { get; } = [];
    public bool HealthChanged { get; internal set; }
    public decimal HealthRestored { get; internal set; }
    public bool RecoveryCompleted { get; internal set; }
    public bool StateChanged => Events.Count > 0;
}

public sealed class CombatService(GameDatabase database)
{
    private int CompletedCultivationLevels(CharacterState character) =>
        character.Cultivation.StageIndex * (database.Cultivation.LevelMultipliers.Count - 1) +
        character.Cultivation.Level - 1;

    public decimal GetHeroMaximumHealth(CharacterState character) =>
        database.Combat.HeroBaseHealth +
        character.Cultivation.StageIndex * database.Combat.HeroHealthPerStage +
        CompletedCultivationLevels(character) * database.Combat.HeroHealthPerLevel;

    public decimal GetHeroAttack(CharacterState character) =>
        database.Combat.HeroBaseAttack +
        character.Cultivation.StageIndex * database.Combat.HeroAttackPerStage +
        CompletedCultivationLevels(character) * database.Combat.HeroAttackPerLevel;

    public decimal GetHeroDefense(CharacterState character) =>
        database.Combat.HeroBaseDefense +
        character.Cultivation.StageIndex * database.Combat.HeroDefensePerStage +
        CompletedCultivationLevels(character) * database.Combat.HeroDefensePerLevel;

    public decimal GetRecoveryHealthThreshold(CharacterState character) =>
        GetHeroMaximumHealth(character) * database.Combat.RecoveryHealthFraction;

    public void ConfigureHero(CharacterState character, bool fillIfUninitialized = false) =>
        character.ConfigureMaximumHealth(GetHeroMaximumHealth(character), fillIfUninitialized);

    public CombatUpdate Update(GameState state, float deltaTime)
    {
        var result = new CombatUpdate();
        ConfigureHero(state.Character);
        var mission = state.CurrentMission;
        if (mission?.Combat is null && state.Character.Health < state.Character.MaximumHealth && deltaTime > 0f)
        {
            var regeneration = Math.Max(0m, ModifierCalculator.Calculate(
                database.Combat.HealthRegenerationPerSecond,
                state.ActiveEffects.Where(effect => !effect.IsExpired),
                EffectType.HealthRegeneration));
            var before = state.Character.Health;
            state.Character.Heal(regeneration * (decimal)Math.Clamp(deltaTime, 0f, 0.25f));
            result.HealthRestored = state.Character.Health - before;
            result.HealthChanged = result.HealthRestored > 0m;
            if (state.RecoveryRequired && state.Character.Health >= GetRecoveryHealthThreshold(state.Character))
            {
                state.CompleteDefeatRecovery();
                result.RecoveryCompleted = true;
            }
        }
        if (mission is null)
            return result;

        if (state.ActivityMode == ActivityMode.Missions && mission.Combat is null && mission.Encounter is { Resolved: false } encounter &&
            mission.CurrentProgress >= encounter.TriggerProgress)
        {
            var monster = database.GetMonster(encounter.MonsterConfigId);
            var multiplier = database.GetDanger(encounter.DangerLevel).MonsterPowerMultiplier;
            var maximumHealth = decimal.Round(monster.MaximumHealth * multiplier, 2);
            var combat = new ActiveCombat
            {
                MonsterConfigId = monster.Id,
                BackgroundId = encounter.BackgroundId,
                DangerLevel = encounter.DangerLevel,
                EnemyMaximumHealth = maximumHealth
            };
            combat.Initialize(maximumHealth, 0.35f, 0.7f);
            mission.StartCombat(combat);
            result.Events.Add(new CombatEvent(CombatEventType.Started));
        }

        var active = mission.Combat;
        if (active is null)
            return result;
        if (active.IsFinished)
        {
            if (!active.AdvanceFinishDelay(deltaTime))
                return result;
            var victory = active.Phase == CombatPhase.Victory;
            mission.ResolveCombat();
            if (!victory)
            {
                state.RemoveMission(mission.InstanceId);
                state.Character.RestoreHealth(0m, state.Character.MaximumHealth);
                state.BeginDefeatRecovery();
            }
            result.Events.Add(new CombatEvent(CombatEventType.Closed));
            return result;
        }

        active.AdvanceCooldowns(Math.Clamp(deltaTime, 0f, 0.25f));
        var monsterConfig = database.GetMonster(active.MonsterConfigId);
        var dangerMultiplier = database.GetDanger(active.DangerLevel).MonsterPowerMultiplier;

        while (active.HeroCooldown <= 0f && active.Phase == CombatPhase.Fighting)
        {
            var damage = Math.Max(1m, GetHeroAttack(state.Character) - monsterConfig.Defense * dangerMultiplier);
            var appliedDamage = active.DamageEnemy(damage);
            active.ResetCooldown(CombatActor.Hero, 1f / database.Combat.HeroAttacksPerSecond);
            result.Events.Add(new CombatEvent(CombatEventType.HeroAttack, appliedDamage));
            result.Events.Add(new CombatEvent(CombatEventType.EnemyHurt, appliedDamage));
            if (active.EnemyHealth <= 0m)
            {
                active.Finish(CombatPhase.Victory, database.Combat.FinishDelaySeconds);
                result.Events.Add(new CombatEvent(CombatEventType.EnemyDied));
                result.Events.Add(new CombatEvent(CombatEventType.Victory));
            }
        }

        while (active.EnemyCooldown <= 0f && active.Phase == CombatPhase.Fighting)
        {
            var damage = Math.Max(1m, monsterConfig.Attack * dangerMultiplier - GetHeroDefense(state.Character));
            var appliedDamage = state.Character.TakeDamage(damage);
            active.ResetCooldown(CombatActor.Enemy, 1f / monsterConfig.AttacksPerSecond);
            result.Events.Add(new CombatEvent(CombatEventType.EnemyAttack, appliedDamage));
            result.Events.Add(new CombatEvent(CombatEventType.HeroHurt, appliedDamage));
            if (state.Character.Health <= 0m)
            {
                active.Finish(CombatPhase.Defeat, database.Combat.FinishDelaySeconds);
                result.Events.Add(new CombatEvent(CombatEventType.HeroDied));
                result.Events.Add(new CombatEvent(CombatEventType.Defeat));
            }
        }
        return result;
    }
}

public sealed class TickProcessor(
    GameDatabase database,
    ItemEffectService effects,
    MissionService missions,
    ShopService shop,
    CultivationService cultivation)
{
    public TickResult ProcessTick(GameState state)
    {
        var modifiers = effects.CalculateModifiers(state);
        var missionProgress = 0m;
        var missionCompleted = false;
        var spiritualPower = 0m;
        var levelsGained = 0;
        if (state.ActivityMode == ActivityMode.Missions && state.CurrentMission?.IsInCombat != true)
        {
            missionProgress = modifiers.TickEfficiency * modifiers.MissionProgressMultiplier;
            missionCompleted = missions.AdvanceCurrentMission(state, missionProgress);
        }
        else if (state.ActivityMode == ActivityMode.Cultivation)
        {
            spiritualPower = database.Balance.BaseSpiritualPowerPerTick *
                             modifiers.TickEfficiency *
                             modifiers.SpiritualPowerMultiplier;
            state.Character.AddSpiritualPower(spiritualPower);
            levelsGained = cultivation.AdvanceLevelsAutomatically(state.Character);
        }
        state.Character.Age.Advance(modifiers.AgingMultiplier, state.Calendar.TicksPerYear);
        var characterDied = state.Character.Age.TotalYears >= cultivation.GetMaximumAge(state.Character);
        effects.AdvanceTemporaryEffects(state);
        var newYear = state.Calendar.AdvanceTick();
        if (newYear)
        {
            shop.Refresh(state.Shop);
            missions.Refresh(state);
        }
        return new TickResult(
            state.Calendar.TotalTicks,
            state.Calendar.CurrentYear,
            spiritualPower,
            missionProgress,
            missionCompleted,
            levelsGained,
            newYear,
            characterDied);
    }

    public TapResult ProcessTap(GameState state)
    {
        var modifiers = effects.CalculateModifiers(state);
        var spiritualPower = database.Balance.BaseSpiritualPowerPerTick *
                             modifiers.TickEfficiency *
                             modifiers.SpiritualPowerMultiplier;
        state.Character.AddSpiritualPower(spiritualPower);
        var levelsGained = cultivation.AdvanceLevelsAutomatically(state.Character);
        return new TapResult(spiritualPower, levelsGained);
    }
}
