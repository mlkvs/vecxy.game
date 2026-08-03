using Vecxy.Assets;

namespace HardCore.Cultivation;

public sealed class CraftingConfig : IYamlConfig
{
    public List<CraftingRecipe> Recipes { get; set; } = [];
}

public sealed class CraftingRecipe
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public int RequiredAlchemyLevel { get; set; } = 1;
    public int DurationTicks { get; set; } = 1;
    public double ExperienceReward { get; set; } = 1.0;
    public List<ItemStack> Ingredients { get; set; } = [];
    public List<ItemStack> Results { get; set; } = [];
}
