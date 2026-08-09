using HardCore.Cultivation.Game.Domain;
using HardCore.Cultivation.Game.Infrastructure;
using GameState = HardCore.Cultivation.Game.Domain.GameState;

namespace HardCore.Cultivation.Game.Application;

public enum AlchemyMode
{
    Pill,
    Distillation
}

public readonly record struct AlchemySelection(Guid InstanceId, int Quantity);

public static class AlchemyCharacteristicFormula
{
    public static decimal Calculate(decimal quality, int ingredientCount, decimal coefficient)
    {
        if (quality < 0m)
            throw new ArgumentOutOfRangeException(nameof(quality));
        if (ingredientCount < 0)
            throw new ArgumentOutOfRangeException(nameof(ingredientCount));
        if (coefficient < 0m)
            throw new ArgumentOutOfRangeException(nameof(coefficient));
        return quality * ingredientCount * (1m + coefficient * ingredientCount);
    }
}

public sealed record AlchemyPreview(
    bool CanCraft,
    string Message,
    ItemInstance? Output,
    IReadOnlyList<string> PropertyNames)
{
    public static AlchemyPreview Fail(string message) => new(false, message, null, []);
}

public sealed record AlchemyCraftResult(bool Success, string Message, ItemInstance? Output)
{
    public static AlchemyCraftResult Fail(string message) => new(false, message, null);
}

public sealed class AlchemyService(GameDatabase database)
{
    private sealed record IngredientUnit(ItemInstance Item, ItemConfig Config);

    public AlchemyPreview Preview(
        GameState state,
        IReadOnlyCollection<AlchemySelection> selection,
        AlchemyMode mode)
    {
        if (!database.Alchemy.Enabled)
            return AlchemyPreview.Fail("Алхимия отключена в конфигурации.");
        var resolved = Resolve(state, selection);
        if (resolved.Error is not null)
            return AlchemyPreview.Fail(resolved.Error);
        return mode == AlchemyMode.Pill
            ? PreviewPill(resolved.Units)
            : PreviewDistillation(resolved.Units);
    }

    public AlchemyCraftResult Craft(
        GameState state,
        IReadOnlyCollection<AlchemySelection> selection,
        AlchemyMode mode)
    {
        var preview = Preview(state, selection, mode);
        if (!preview.CanCraft || preview.Output is null)
            return AlchemyCraftResult.Fail(preview.Message);

        foreach (var selected in selection
                     .GroupBy(value => value.InstanceId)
                     .Select(group => new AlchemySelection(group.Key, group.Sum(value => value.Quantity))))
        {
            if (!state.Inventory.Remove(selected.InstanceId, selected.Quantity))
                return AlchemyCraftResult.Fail("Состав инвентаря изменился. Соберите смесь заново.");
        }

        var output = preview.Output.Copy();
        var stored = state.Inventory.Items.FirstOrDefault(candidate => candidate.CanStackWith(output));
        state.Inventory.Add(output);
        stored ??= output;
        return new AlchemyCraftResult(true, $"Создано: {output.CustomName ?? database.GetItem(output.ConfigId).Name}", stored);
    }

    public IReadOnlyList<AlchemyPropertyAmount> GetProperties(ItemInstance item)
    {
        if (item.AlchemyProperties.Count > 0)
            return item.AlchemyProperties;
        return database.GetItem(item.ConfigId).AlchemyProperties;
    }

    private (List<IngredientUnit> Units, string? Error) Resolve(
        GameState state,
        IReadOnlyCollection<AlchemySelection> selection)
    {
        var normalized = selection
            .Where(value => value.Quantity > 0)
            .GroupBy(value => value.InstanceId)
            .Select(group => new AlchemySelection(group.Key, group.Sum(value => value.Quantity)))
            .ToArray();
        var total = normalized.Sum(value => value.Quantity);
        if (total <= 0)
            return ([], "Добавьте сырьё в алхимическую схему.");
        if (total > database.Alchemy.MaximumIngredients + 1)
            return ([], "В алхимической схеме недостаточно свободных точек.");

        var units = new List<IngredientUnit>(total);
        foreach (var selected in normalized)
        {
            var item = state.Inventory.Find(selected.InstanceId);
            if (item is null || item.Quantity < selected.Quantity)
                return ([], "Один из выбранных ингредиентов закончился.");
            var config = database.GetItem(item.ConfigId);
            if (config.Category is not (ItemCategory.Ingredient or ItemCategory.Core) ||
                config.Category == ItemCategory.Ingredient && GetProperties(item).Count == 0)
                return ([], $"{item.CustomName ?? config.Name} нельзя использовать в алхимии.");
            for (var index = 0; index < selected.Quantity; index++)
                units.Add(new IngredientUnit(item, config));
        }
        return (units, null);
    }

    private AlchemyPreview PreviewPill(IReadOnlyList<IngredientUnit> units)
    {
        var cores = units.Where(unit => unit.Config.Category == ItemCategory.Core).ToArray();
        var ingredients = units.Where(unit => unit.Config.Category == ItemCategory.Ingredient).ToArray();
        if (cores.Length != 1)
            return AlchemyPreview.Fail("Для рецепта требуется ровно одно ядро в центральной точке.");
        if (ingredients.Length < database.Alchemy.MinimumIngredients)
            return AlchemyPreview.Fail($"Добавьте минимум {database.Alchemy.MinimumIngredients} ингредиента вокруг ядра.");
        if (ingredients.Length > database.Alchemy.MaximumIngredients)
            return AlchemyPreview.Fail($"В рецепте может быть не больше {database.Alchemy.MaximumIngredients} ингредиентов.");

        var ingredientQuality = ingredients.Average(value => value.Item.Quality);
        var quality = ingredientQuality * (1m - database.Alchemy.CoreQualityWeight) +
                      cores[0].Item.Quality * database.Alchemy.CoreQualityWeight;
        quality = Math.Clamp(quality, 0.1m, database.Alchemy.MaximumQuality);
        var characteristicMultiplier = AlchemyCharacteristicFormula.Calculate(
            quality,
            ingredients.Length,
            database.Alchemy.IngredientCharacteristicCoefficient);

        var effects = new List<(ItemEffectDefinition Effect, AlchemyPropertyConfig Property, int Matches)>();
        foreach (var propertyId in ingredients
                     .SelectMany(unit => GetProperties(unit.Item))
                     .Select(value => value.PropertyId)
                     .Distinct(StringComparer.Ordinal))
        {
            var contributions = ingredients
                .Select(unit => GetProperties(unit.Item).FirstOrDefault(value => value.PropertyId == propertyId))
                .Where(value => value is not null)
                .Cast<AlchemyPropertyAmount>()
                .ToArray();
            var coverage = contributions.Length / (decimal)ingredients.Length;
            if (contributions.Length < database.Alchemy.MinimumPropertyMatches ||
                coverage < database.Alchemy.MinimumPropertyFraction)
                continue;
            var property = database.GetAlchemyProperty(propertyId);
            var potency = contributions.Average(value => value.Potency);
            effects.Add((new ItemEffectDefinition
            {
                Type = property.EffectType,
                Operation = property.Operation,
                Value = property.BaseValue * potency * coverage * characteristicMultiplier
            }, property, contributions.Length));
        }

        var selectedEffects = effects
            .OrderByDescending(value => value.Matches)
            .ThenByDescending(value => Math.Abs(value.Effect.Value))
            .Take(database.Alchemy.MaximumPillEffects)
            .ToArray();
        if (selectedEffects.Length == 0)
            return AlchemyPreview.Fail(
                $"Ни одно свойство не повторяется хотя бы {database.Alchemy.MinimumPropertyMatches} раза.");

        var rarity = AverageRarity(units.Select(value => value.Item.Rarity));
        var name = selectedEffects.Length == 1
            ? selectedEffects[0].Property.PillName
            : $"Составная пилюля · {selectedEffects.Length} эффекта";
        var output = new ItemInstance
        {
            InstanceId = Guid.Empty,
            ConfigId = database.Alchemy.CraftedPillItemId,
            Rarity = rarity,
            Quality = quality,
            CustomName = name,
            CustomDescription = $"Создана из алхимических свойств ингредиентов с ядром «{cores[0].Item.CustomName ?? cores[0].Config.Name}».",
            CraftedDurationTicks = database.Alchemy.PillDurationTicks,
            CraftedEffects = selectedEffects.Select(value => value.Effect).ToList()
        };
        return new AlchemyPreview(
            true,
            selectedEffects.Length == 1 ? "Смесь устойчива." : "Свойства образуют многокомпонентную пилюлю.",
            output,
            selectedEffects.Select(value => value.Property.DisplayName).ToArray());
    }

    private AlchemyPreview PreviewDistillation(IReadOnlyList<IngredientUnit> units)
    {
        if (units.Any(unit => unit.Config.Category != ItemCategory.Ingredient))
            return AlchemyPreview.Fail("Ядра нельзя использовать при рафинировании.");
        if (units.Count < database.Alchemy.MinimumIngredients)
            return AlchemyPreview.Fail($"Добавьте минимум {database.Alchemy.MinimumIngredients} одинаковых ингредиента.");
        if (units.Count > database.Alchemy.MaximumIngredients)
            return AlchemyPreview.Fail($"Для рафинирования можно взять не больше {database.Alchemy.MaximumIngredients} ингредиентов.");
        var origin = units[0].Item.AlchemyOriginId ?? units[0].Item.ConfigId;
        var level = units[0].Item.DistillationLevel;
        if (units.Any(unit => (unit.Item.AlchemyOriginId ?? unit.Item.ConfigId) != origin ||
                              unit.Item.DistillationLevel != level))
            return AlchemyPreview.Fail("Для рафинирования требуется одинаковое сырьё одной ступени.");

        var nextLevel = level + 1;
        var potencyMultiplier = 1m + database.Alchemy.DistillationPotencyPerLevel * nextLevel;
        var properties = units
            .SelectMany(unit => GetProperties(unit.Item))
            .GroupBy(value => value.PropertyId, StringComparer.Ordinal)
            .Select(group => new AlchemyPropertyAmount
            {
                PropertyId = group.Key,
                Potency = group.Average(value => value.Potency) * potencyMultiplier
            })
            .OrderBy(value => value.PropertyId, StringComparer.Ordinal)
            .ToList();
        var averageQuality = units.Average(value => value.Item.Quality);
        var quality = averageQuality +
                      database.Alchemy.DistillationQualityPerIngredient * units.Count +
                      database.Alchemy.DistillationQualityPerLevel * level;
        var sourceName = database.GetItem(origin).Name;
        var name = nextLevel == 1
            ? $"Экстракт · {sourceName}"
            : $"Мощный экстракт {ToRoman(nextLevel)} · {sourceName}";
        var output = new ItemInstance
        {
            InstanceId = Guid.Empty,
            ConfigId = database.Alchemy.ExtractItemId,
            Rarity = AverageRarity(units.Select(value => value.Item.Rarity)),
            Quality = Math.Clamp(quality, 0.1m, database.Alchemy.MaximumQuality),
            CustomName = name,
            CustomDescription = $"Очищенное сырьё ступени {nextLevel}. Сохраняет свойства исходного ингредиента.",
            AlchemyOriginId = origin,
            DistillationLevel = nextLevel,
            AlchemyProperties = properties
        };
        return new AlchemyPreview(
            true,
            $"Качество повысится с {averageQuality:0.0} до {output.Quality:0.0}.",
            output,
            properties.Select(value => database.GetAlchemyProperty(value.PropertyId).DisplayName).ToArray());
    }

    private static ItemRarity AverageRarity(IEnumerable<ItemRarity> rarities)
    {
        var values = rarities.Select(value => (int)value).ToArray();
        var average = decimal.Round(values.Select(value => (decimal)value).Average(), 0, MidpointRounding.AwayFromZero);
        return (ItemRarity)Math.Clamp((int)average, 0, Enum.GetValues<ItemRarity>().Length - 1);
    }

    private static string ToRoman(int value) => value switch
    {
        1 => "I", 2 => "II", 3 => "III", 4 => "IV", 5 => "V",
        _ => value.ToString()
    };
}
