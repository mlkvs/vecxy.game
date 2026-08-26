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

public static class AlchemyResultRankFormula
{
    public static decimal Calculate(
        decimal coreRank,
        IReadOnlyCollection<decimal> ingredientRanks,
        decimal averageWeight,
        decimal maximumWeight,
        decimal coreRankWeight,
        decimal baseSigma,
        decimal randomnessReferenceIngredientCount,
        decimal standardNormal,
        decimal minimumAllowed,
        decimal maximumAllowed)
    {
        if (ingredientRanks.Count == 0)
            throw new ArgumentException("At least one ingredient rank is required.", nameof(ingredientRanks));
        if (averageWeight < 0m || maximumWeight < 0m || averageWeight + maximumWeight <= 0m)
            throw new ArgumentOutOfRangeException(nameof(averageWeight));
        if (coreRankWeight <= 0m || baseSigma < 0m || randomnessReferenceIngredientCount <= 0m)
            throw new ArgumentOutOfRangeException(nameof(coreRankWeight));
        if (maximumAllowed < minimumAllowed)
            throw new ArgumentOutOfRangeException(nameof(maximumAllowed));

        var allRanks = ingredientRanks.Append(coreRank).ToArray();
        var weightedAverage = (coreRankWeight * coreRank + ingredientRanks.Sum()) /
                              (coreRankWeight + ingredientRanks.Count);
        var sigma = baseSigma * (decimal)Math.Sqrt(
            (double)(randomnessReferenceIngredientCount / ingredientRanks.Count));
        var unrounded = averageWeight * weightedAverage +
                        maximumWeight * allRanks.Max() +
                        sigma * standardNormal;
        var rounded = decimal.Round(unrounded, 0, MidpointRounding.AwayFromZero);
        var componentMinimum = allRanks.Min() - 1m;
        var componentMaximum = allRanks.Max() + 1m;
        return Math.Clamp(
            rounded,
            Math.Max(minimumAllowed, componentMinimum),
            Math.Min(maximumAllowed, componentMaximum));
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

public sealed record AlchemyCraftResult(
    bool Success,
    string Message,
    IReadOnlyList<ItemInstance> Outputs,
    int ProducedQuantity,
    bool IngredientsDestroyed = false,
    decimal SuccessChancePercent = 0m)
{
    public ItemInstance? Output => Outputs.FirstOrDefault();
    public static AlchemyCraftResult Fail(string message) => new(false, message, [], 0);
}

public sealed class AlchemyService(GameDatabase database, IRandomSource random)
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
            ? PreviewPill(resolved.Units, resolveMixedPurification: false)
            : PreviewDistillation(resolved.Units);
    }

    public AlchemyCraftResult Craft(
        GameState state,
        IReadOnlyCollection<AlchemySelection> selection,
        AlchemyMode mode)
    {
        var resolved = Resolve(state, selection);
        if (resolved.Error is not null)
            return AlchemyCraftResult.Fail(resolved.Error);
        var validation = mode == AlchemyMode.Pill
            ? PreviewPill(resolved.Units, resolveMixedPurification: false)
            : PreviewDistillation(resolved.Units);
        if (!validation.CanCraft || validation.Output is null)
            return AlchemyCraftResult.Fail(validation.Message);

        var successChance = mode == AlchemyMode.Distillation
            ? 100m
            : CalculateSuccessChance(resolved.Units, mode);
        if (mode == AlchemyMode.Pill && random.NextDecimal(0m, 100m) >= successChance)
        {
            if (!RemoveSelected(state, selection))
                return AlchemyCraftResult.Fail("Состав инвентаря изменился. Соберите смесь заново.");
            return new AlchemyCraftResult(
                false,
                $"Неудача: шанс успеха был {successChance:0.#}%. Все ингредиенты уничтожены.",
                [],
                0,
                IngredientsDestroyed: true,
                SuccessChancePercent: successChance);
        }

        var previews = mode == AlchemyMode.Pill
            ? CreatePillBatch(resolved.Units)
            : [PreviewDistillation(resolved.Units)];
        var failedPreview = previews.FirstOrDefault(preview => !preview.CanCraft || preview.Output is null);
        if (failedPreview is not null)
            return AlchemyCraftResult.Fail(failedPreview.Message);

        if (!RemoveSelected(state, selection))
            return AlchemyCraftResult.Fail("Состав инвентаря изменился. Соберите смесь заново.");

        var storedOutputs = new List<ItemInstance>();
        foreach (var preview in previews)
        {
            var output = preview.Output!.Copy();
            var stored = state.Inventory.Items.FirstOrDefault(candidate => candidate.CanStackWith(output));
            state.Inventory.Add(output);
            stored ??= output;
            if (!storedOutputs.Contains(stored))
                storedOutputs.Add(stored);
        }
        var first = storedOutputs[0];
        var producedQuantity = previews.Count;
        var name = first.CustomName ?? database.GetItem(first.ConfigId).Name;
        return new AlchemyCraftResult(true, $"Создано: {name} x{producedQuantity}", storedOutputs, producedQuantity,
            SuccessChancePercent: successChance);
    }

    private decimal CalculateSuccessChance(IReadOnlyList<IngredientUnit> units, AlchemyMode mode)
    {
        var ingredients = units.Where(unit => unit.Config.Category == ItemCategory.Ingredient).ToArray();
        var coreQuality = mode == AlchemyMode.Pill
            ? units.Single(unit => unit.Config.Category == ItemCategory.Core).Item.Quality
            : 0m;
        var ingredientQuality = 0.4m * ingredients.Average(unit => unit.Item.Quality) +
                                0.6m * ingredients.Max(unit => unit.Item.Quality);
        return Math.Min(
            database.Alchemy.MaximumCraftSuccessChance,
            database.Alchemy.CraftSuccessChancePerQuality * (coreQuality + ingredientQuality));
    }

    private static bool RemoveSelected(GameState state, IReadOnlyCollection<AlchemySelection> selection)
    {
        foreach (var selected in selection
                     .GroupBy(value => value.InstanceId)
                     .Select(group => new AlchemySelection(group.Key, group.Sum(value => value.Quantity))))
        {
            if (!state.Inventory.Remove(selected.InstanceId, selected.Quantity))
                return false;
        }
        return true;
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

    private IReadOnlyList<AlchemyPreview> CreatePillBatch(IReadOnlyList<IngredientUnit> units)
    {
        var validation = PreviewPill(units, resolveMixedPurification: false);
        if (!validation.CanCraft)
            return [validation];
        var quantityChance = WeightedRandom.Select(
            database.Alchemy.PillOutputQuantityChances,
            chance => chance.ChancePercent,
            random);
        var forcePurity = RollMixedPurificationResult(units);
        return Enumerable.Range(0, quantityChance.Quantity)
            .Select(_ => PreviewPill(units, resolveMixedPurification: true, rollRanks: true, forcePurity,
                rollProperties: true))
            .ToArray();
    }

    private bool? RollMixedPurificationResult(IReadOnlyList<IngredientUnit> units)
    {
        var ingredients = units.Where(unit => unit.Config.Category == ItemCategory.Ingredient).ToArray();
        var purificationCount = ingredients.Count(unit => GetProperties(unit.Item)
            .Any(property => property.PropertyId == database.Alchemy.PurificationPropertyId));
        if (purificationCount == 0 || purificationCount == ingredients.Length)
            return null;
        return random.NextDecimal(0m, 1m) >= database.Alchemy.PurificationMixedRecipeChance;
    }

    private AlchemyPreview PreviewPill(
        IReadOnlyList<IngredientUnit> units,
        bool resolveMixedPurification,
        bool rollRanks = false,
        bool? forcePurity = null,
        bool rollProperties = false)
    {
        var cores = units.Where(unit => unit.Config.Category == ItemCategory.Core).ToArray();
        var ingredients = units.Where(unit => unit.Config.Category == ItemCategory.Ingredient).ToArray();
        if (cores.Length != 1)
            return AlchemyPreview.Fail("Для рецепта требуется ровно одно ядро в центральной точке.");
        if (ingredients.Length < database.Alchemy.MinimumIngredients)
            return AlchemyPreview.Fail($"Добавьте минимум {database.Alchemy.MinimumIngredients} ингредиента вокруг ядра.");
        if (ingredients.Length > database.Alchemy.MaximumIngredients)
            return AlchemyPreview.Fail($"В рецепте может быть не больше {database.Alchemy.MaximumIngredients} ингредиентов.");

        var quality = CalculateResultRank(
            cores[0].Item.Quality,
            ingredients.Select(value => value.Item.Quality).ToArray(),
            database.Alchemy.QualityRandomnessSigma,
            rollRanks,
            0.1m,
            database.Alchemy.MaximumQuality);
        var rarityRank = CalculateResultRank(
            (int)cores[0].Item.Rarity,
            ingredients.Select(value => (decimal)(int)value.Item.Rarity).ToArray(),
            database.Alchemy.RarityRandomnessSigma,
            rollRanks,
            0m,
            Enum.GetValues<ItemRarity>().Length - 1);
        var rarity = (ItemRarity)(int)rarityRank;
        // The spreadsheet scales potion effects only by the resulting quality and rarity.
        // Ingredient count influences eligibility and property coverage, not the numeric value exponentially.
        var characteristicMultiplier = ItemBalanceFormula.GetQualityMultiplier(
                                         database.Balance,
                                         ItemCategory.Pill,
                                         quality) *
                                     database.GetRarity(rarity).PriceMultiplier;
        var elementModifier = ElementCompatibilityCalculator.GetModifier(
            cores[0].Config.Element,
            ingredients.Select(unit => unit.Config.Element),
            database.Alchemy);
        var contamination = ContaminationCalculator.Combine(ingredients.Select(unit =>
            unit.Config.Category == ItemCategory.Ingredient &&
            unit.Config.AlchemyProperties.Any(property => property.PropertyId == database.Alchemy.PurificationPropertyId)
                ? 0m
                : Math.Clamp(unit.Item.Contamination, 0m, 1m)),
            database.Balance.ContaminationCombinationDivisor);
        var contaminationModifier = new PiecewiseLinearCurve<ContaminationCurvePoint>(database.Alchemy.ContaminationModifierCurve,
            point => point.Contamination, point => point.Multiplier).Evaluate(contamination);

        var purificationIngredients = ingredients.Where(unit => GetProperties(unit.Item)
            .Any(property => property.PropertyId == database.Alchemy.PurificationPropertyId)).ToArray();
        var hasPurification = purificationIngredients.Length > 0;
        var allPurification = purificationIngredients.Length == ingredients.Length;
        if (allPurification)
            return CreatePurityPill(units, quality, rarity, contamination, resolveMixedPurification);

        var effects = new List<(ItemEffectDefinition Effect, AlchemyPropertyConfig Property, int Matches)>();
        foreach (var propertyId in ingredients
                     .SelectMany(unit => GetProperties(unit.Item))
                     .Select(value => value.PropertyId)
                     .Where(propertyId => propertyId != database.Alchemy.PurificationPropertyId)
                     .Distinct(StringComparer.Ordinal))
        {
            var contributions = ingredients
                .Select(unit => GetProperties(unit.Item).FirstOrDefault(value => value.PropertyId == propertyId))
                .Where(value => value is not null)
                .Cast<AlchemyPropertyAmount>()
                .ToArray();
            var chance = contributions.Length / (decimal)ingredients.Length;
            if (rollProperties && random.NextDecimal(0m, 1m) >= chance)
                continue;
            var property = database.GetAlchemyProperty(propertyId);
            effects.Add((new ItemEffectDefinition
            {
                Type = property.EffectType,
                Operation = property.Operation,
                Value = property.BaseValue * chance * characteristicMultiplier * elementModifier * contaminationModifier
            }, property, contributions.Length));
        }

        var selectedEffects = effects
            .OrderByDescending(value => value.Matches)
            .ThenByDescending(value => Math.Abs(value.Effect.Value))
            .Take(1)
            .ToArray();
        if (selectedEffects.Length == 0)
            return hasPurification
                ? CreatePurityPill(units, quality, rarity, contamination, resolveMixedPurification)
                : AlchemyPreview.Fail(
                    "Ни одно свойство не выпало по вероятности ингредиентов.");

        var output = new ItemInstance
        {
            InstanceId = Guid.Empty,
            ConfigId = selectedEffects[0].Property.ResultPillItemId,
            Rarity = rarity,
            Quality = quality,
            Contamination = contamination,
            CraftedEffects = [selectedEffects[0].Effect]
        };
        if (hasPurification)
        {
            if (!resolveMixedPurification)
                return new AlchemyPreview(
                    true,
                    $"Смесь с шансом {database.Alchemy.PurificationMixedRecipeChance:P0} даст обычную пилюлю, иначе получится пилюля чистоты.",
                    CreatePurityPill(units, quality, rarity, contamination, randomizePercent: false).Output,
                    ["Очищение", .. selectedEffects.Select(value => value.Property.DisplayName)]);
            if (forcePurity ?? random.NextDecimal(0m, 1m) >= database.Alchemy.PurificationMixedRecipeChance)
                return CreatePurityPill(units, quality, rarity, contamination, randomizePercent: true);
        }

        return new AlchemyPreview(
            true,
            "Смесь устойчива.",
            output,
            selectedEffects.Select(value => value.Property.DisplayName).ToArray());
    }

    private decimal CalculateResultRank(
        decimal coreRank,
        IReadOnlyCollection<decimal> ingredientRanks,
        decimal baseSigma,
        bool rollRandomness,
        decimal minimumAllowed,
        decimal maximumAllowed) =>
        AlchemyResultRankFormula.Calculate(
            coreRank,
            ingredientRanks,
            database.Alchemy.ResultAverageWeight,
            database.Alchemy.ResultMaximumWeight,
            database.Alchemy.CoreRankWeight,
            baseSigma,
            database.Alchemy.RandomnessReferenceIngredientCount,
            rollRandomness ? NextStandardNormal() : 0m,
            minimumAllowed,
            maximumAllowed);

    private decimal NextStandardNormal()
    {
        var first = Math.Max(1e-12, (double)random.NextDecimal(0m, 1m));
        var second = (double)random.NextDecimal(0m, 1m);
        return (decimal)(Math.Sqrt(-2d * Math.Log(first)) * Math.Cos(2d * Math.PI * second));
    }

    private AlchemyPreview CreatePurityPill(
        IReadOnlyList<IngredientUnit> units,
        decimal quality,
        ItemRarity rarity,
        decimal contamination,
        bool randomizePercent)
    {
        var percent = randomizePercent
            ? RollPurificationPercent()
            : (database.Alchemy.PurificationMinimumPercent + database.Alchemy.PurificationMaximumPercent) / 2m;
        var output = new ItemInstance
        {
            InstanceId = Guid.Empty,
            ConfigId = database.GetAlchemyProperty(database.Alchemy.PurificationPropertyId).ResultPillItemId,
            Rarity = rarity,
            Quality = quality,
            Contamination = contamination,
            PurificationPercent = percent,
            CustomName = "Пилюля чистоты",
            CustomDescription = $"Очищает тело на {percent:0.#}% до передачи собственного загрязнения.",
            CraftedEffects = [new ItemEffectDefinition { Type = EffectType.PurifyContamination, Operation = ModifierOperation.Flat, Value = percent }]
        };
        return new AlchemyPreview(true, $"Будет создана пилюля чистоты ({percent:0.#}% очищения).", output, ["Очищение"]);
    }

    private decimal RollPurificationPercent() => database.Alchemy.PurificationMaximumPercent == database.Alchemy.PurificationMinimumPercent
        ? database.Alchemy.PurificationMinimumPercent
        : random.NextDecimal(database.Alchemy.PurificationMinimumPercent, database.Alchemy.PurificationMaximumPercent);

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
        var properties = units
            .SelectMany(unit => GetProperties(unit.Item))
            .GroupBy(value => value.PropertyId, StringComparer.Ordinal)
            .Select(group => new AlchemyPropertyAmount
            {
                PropertyId = group.Key
            })
            .OrderBy(value => value.PropertyId, StringComparer.Ordinal)
            .ToList();
        var averageQuality = units.Average(value => value.Item.Quality);
        var contamination = ContaminationCalculator.Combine(
            units.Select(value => Math.Clamp(value.Item.Contamination, 0m, 1m)),
            database.Balance.ContaminationCombinationDivisor);
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
            Contamination = contamination,
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
