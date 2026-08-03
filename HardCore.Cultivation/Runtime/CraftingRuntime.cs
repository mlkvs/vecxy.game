namespace HardCore.Cultivation;

public sealed class CraftingRuntime
{
    public string RecipeId { get; set; } = string.Empty;
    public int RemainingTicks { get; set; }
    public int Amount { get; set; } = 1;
}
