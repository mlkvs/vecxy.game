namespace HardCore.Cultivation;

public sealed class CraftingSystem
(
    GameDatabase database,
    Inventory inventory,
    PlayerProgress progress
)
{
    private readonly List<CraftingRuntime> _queue = [];

    public event Action? QueueChanged;
    public event Action<CraftingRecipe, int>? CraftCompleted;

    public IReadOnlyList<CraftingRuntime> Queue => _queue;

    public bool CanCraft(string recipeId, int amount = 1)
    {
        if (amount <= 0)
            return false;

        var recipe = database.GetRecipe(recipeId);

        if (progress.AlchemyLevel < recipe.RequiredAlchemyLevel)
            return false;

        return recipe.Ingredients.All(x =>
            inventory.Has(x.ItemId, x.Amount * amount));
    }

    public bool TryCraft(string recipeId, int amount = 1)
    {
        if (!CanCraft(recipeId, amount))
            return false;

        var recipe = database.GetRecipe(recipeId);
        var costs = recipe.Ingredients
            .Select(x => new ItemStack
            {
                ItemId = x.ItemId,
                Amount = x.Amount * amount
            })
            .ToArray();

        if (!inventory.TryRemove(costs))
            return false;

        _queue.Add(new CraftingRuntime
        {
            RecipeId = recipeId,
            Amount = amount,
            RemainingTicks = Math.Max(1, recipe.DurationTicks * amount)
        });

        QueueChanged?.Invoke();
        return true;
    }

    public void Tick()
    {
        if (_queue.Count == 0)
            return;

        var active = _queue[0];
        active.RemainingTicks--;

        if (active.RemainingTicks > 0)
        {
            QueueChanged?.Invoke();
            return;
        }

        var recipe = database.GetRecipe(active.RecipeId);

        foreach (var result in recipe.Results)
            inventory.Add(result.ItemId, result.Amount * active.Amount);

        progress.AlchemyExperience += recipe.ExperienceReward * active.Amount;
        UpdateAlchemyLevel();

        _queue.RemoveAt(0);
        CraftCompleted?.Invoke(recipe, active.Amount);
        QueueChanged?.Invoke();
    }

    public void ReplaceWith(IEnumerable<CraftingRuntime> queue)
    {
        _queue.Clear();
        _queue.AddRange(queue);
    }

    private void UpdateAlchemyLevel()
    {
        while (progress.AlchemyExperience >= GetRequiredExperience(progress.AlchemyLevel))
        {
            progress.AlchemyExperience -= GetRequiredExperience(progress.AlchemyLevel);
            progress.AlchemyLevel++;
        }
    }

    private static double GetRequiredExperience(int level)
    {
        return 25.0 * level * level;
    }
}
