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
            int? duration = config.DurationType == ItemDurationType.Permanent
                ? null
                : config.TemporaryDurationTicks;
            foreach (var definition in config.Effects)
            {
                state.ActiveEffects.Add(new ActiveEffect(
                    config.Id,
                    definition,
                    definition.Value * strength,
                    duration,
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
        state.EnqueueMission(new ActiveMission
        {
            MissionConfigId = missionId,
            RequiredProgress = required
        });
        state.MissionBoard.Take(missionId);
        return TransactionResult.Ok(0, $"Добавлено в очередь: {config.Name}");
    }

    public TransactionResult Remove(GameState state, Guid missionInstanceId) =>
        state.RemoveMission(missionInstanceId)
            ? TransactionResult.Ok(0, "Миссия удалена из очереди.")
            : TransactionResult.Fail("Миссия не найдена.");

    public TransactionResult Move(GameState state, Guid missionInstanceId, int offset) =>
        state.MoveMission(missionInstanceId, offset)
            ? TransactionResult.Ok(0, "Порядок миссий изменён.")
            : TransactionResult.Fail("Миссию нельзя переместить дальше.");

    public bool AdvanceCurrentMission(GameState state, decimal progress)
    {
        var mission = state.CurrentMission;
        if (mission is null || mission.IsCompleted)
            return false;
        mission.AddProgress(progress);
        if (!mission.IsCompleted || mission.RewardGranted)
            return false;

        var config = database.GetMission(mission.MissionConfigId);
        var candidates = database.Items.Values
            .Where(item => config.Reward.RequiredItemCategory is null ||
                           item.Category == config.Reward.RequiredItemCategory)
            .ToArray();
        if (candidates.Length == 0)
            throw new InvalidOperationException($"Mission reward pool is empty: {config.Id}");
        var rewardConfig = WeightedRandom.Select(candidates, item => item.ShopWeight, random);
        var quantity = random.NextInt(
            config.Reward.MinimumQuantity,
            config.Reward.MaximumQuantity + 1);
        state.Inventory.Add(itemGenerator.Generate(rewardConfig.Id, quantity));
        state.Character.AddMoney(config.Reward.Money);
        mission.MarkRewardGranted();
        state.RemoveMission(mission.InstanceId);
        return true;
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
            random.NextInt(database.Shop.MinimumSellAdjustment, database.Shop.MaximumSellAdjustment + 1));
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
            return TransactionResult.Fail("Недостаточно монет.");
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
    public decimal GetRequiredPower(int stageIndex, int level)
    {
        if (level is < 1 or > 10)
            throw new ArgumentOutOfRangeException(nameof(level));
        var stage = database.Cultivation.Stages[stageIndex];
        return database.Cultivation.BaseRequiredPower *
               database.Cultivation.LevelMultipliers[level - 1] *
               stage.StageMultiplier;
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

    public BreakthroughResult AttemptBreakthrough(
        CharacterState character,
        IReadOnlyCollection<ActiveEffect> effects)
    {
        var progress = character.Cultivation;
        if (!progress.CanAttemptBreakthrough)
            return new(false, 0m, progress.StageIndex, progress.Level, "Прорыв доступен только на 10 уровне.");
        if (progress.StageIndex >= database.Cultivation.Stages.Count - 1)
            return new(false, 100m, progress.StageIndex, progress.Level, "Достигнута высшая ступень.");
        var cost = GetRequiredPower(progress.StageIndex, 10);
        if (!character.TrySpendSpiritualPower(cost))
            return new(false, 0m, progress.StageIndex, progress.Level, "Недостаточно духовной силы.");

        var baseChance = database.Cultivation.Stages[progress.StageIndex].BaseBreakthroughChance;
        var chance = Math.Clamp(
            ModifierCalculator.Calculate(baseChance, effects, EffectType.BreakthroughChance),
            0m,
            database.Balance.MaximumBreakthroughChance);
        if (random.NextDecimal(0m, 100m) < chance)
        {
            progress.BreakthroughSucceeded(database.Cultivation.Stages.Count);
            return new(true, chance, progress.StageIndex, progress.Level, "Прорыв успешен.");
        }
        progress.BreakthroughFailed(random.NextInt(1, 10));
        return new(false, chance, progress.StageIndex, progress.Level, "Прорыв не удался.");
    }
}

public sealed class TickProcessor(
    GameDatabase database,
    ItemEffectService effects,
    MissionService missions,
    ShopService shop)
{
    public TickResult ProcessTick(GameState state)
    {
        var modifiers = effects.CalculateModifiers(state);
        var missionProgress = modifiers.TickEfficiency * modifiers.MissionProgressMultiplier;
        var missionCompleted = missions.AdvanceCurrentMission(state, missionProgress);
        var spiritualPower = database.Balance.BaseSpiritualPowerPerTick *
                             modifiers.TickEfficiency *
                             modifiers.SpiritualPowerMultiplier;
        state.Character.AddSpiritualPower(spiritualPower);
        state.Character.Age.Advance(modifiers.AgingMultiplier, state.Calendar.TicksPerYear);
        var characterDied = state.Character.Age.TotalYears >= database.Balance.MaximumAgeYears;
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
            newYear,
            characterDied);
    }
}
