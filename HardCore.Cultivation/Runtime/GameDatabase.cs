using Vecxy.Assets;

namespace HardCore.Cultivation;

public sealed class GameDatabase
{
    private readonly Dictionary<string, ItemInfo> _items = new(StringComparer.Ordinal);
    private readonly Dictionary<string, MissionInfo> _missions = new(StringComparer.Ordinal);
    private readonly Dictionary<string, CraftingRecipe> _recipes = new(StringComparer.Ordinal);
    private readonly Dictionary<ECultivationStage, CultivationStageInfo> _stages = [];

    public IReadOnlyDictionary<string, ItemInfo> Items => _items;
    public IReadOnlyDictionary<string, MissionInfo> Missions => _missions;
    public IReadOnlyDictionary<string, CraftingRecipe> Recipes => _recipes;
    public IReadOnlyDictionary<ECultivationStage, CultivationStageInfo> Stages => _stages;

    public int LevelsPerStage { get; private set; } = 9;

    public void Initialize(
        ConfigRef<ItemsConfig> items,
        ConfigRef<MissionsConfig> missions,
        ConfigRef<CraftingConfig> crafting,
        ConfigRef<CultivationConfig> cultivation)
    {
        _items.Clear();
        _missions.Clear();
        _recipes.Clear();
        _stages.Clear();

        foreach (var item in items.Value.Items)
            AddUnique(_items, item.Id, item, "item");

        foreach (var mission in missions.Value.Missions)
            AddUnique(_missions, mission.Id, mission, "mission");

        foreach (var recipe in crafting.Value.Recipes)
            AddUnique(_recipes, recipe.Id, recipe, "recipe");

        foreach (var stage in cultivation.Value.Stages)
        {
            if (!_stages.TryAdd(stage.Stage, stage))
                throw new InvalidOperationException($"Duplicate cultivation stage: {stage.Stage}");
        }

        LevelsPerStage = Math.Max(1, cultivation.Value.LevelsPerStage);

        ValidateReferences();
    }

    public ItemInfo GetItem(string id)
    {
        return _items.TryGetValue(id, out var value)
            ? value
            : throw new KeyNotFoundException($"Unknown item: {id}");
    }

    public MissionInfo GetMission(string id)
    {
        return _missions.TryGetValue(id, out var value)
            ? value
            : throw new KeyNotFoundException($"Unknown mission: {id}");
    }

    public CraftingRecipe GetRecipe(string id)
    {
        return _recipes.TryGetValue(id, out var value)
            ? value
            : throw new KeyNotFoundException($"Unknown recipe: {id}");
    }

    public CultivationStageInfo GetStage(ECultivationStage stage)
    {
        return _stages.TryGetValue(stage, out var value)
            ? value
            : throw new KeyNotFoundException($"Unknown cultivation stage: {stage}");
    }

    private void ValidateReferences()
    {
        foreach (var mission in _missions.Values)
        {
            ValidateStacks(mission.Costs, $"mission {mission.Id} costs");
            ValidateStacks(mission.Rewards, $"mission {mission.Id} rewards");
        }

        foreach (var recipe in _recipes.Values)
        {
            ValidateStacks(recipe.Ingredients, $"recipe {recipe.Id} ingredients");
            ValidateStacks(recipe.Results, $"recipe {recipe.Id} results");
        }
    }

    private void ValidateStacks(IEnumerable<ItemStack> stacks, string owner)
    {
        foreach (var stack in stacks)
        {
            if (!_items.ContainsKey(stack.ItemId))
                throw new InvalidOperationException($"Unknown item '{stack.ItemId}' in {owner}.");

            if (stack.Amount <= 0)
                throw new InvalidOperationException($"Invalid item amount in {owner}.");
        }
    }

    private static void AddUnique<T>(
        IDictionary<string, T> dictionary,
        string id,
        T value,
        string type)
    {
        if (string.IsNullOrWhiteSpace(id))
            throw new InvalidOperationException($"{type} id is empty.");

        if (!dictionary.TryAdd(id, value))
            throw new InvalidOperationException($"Duplicate {type} id: {id}");
    }
}
