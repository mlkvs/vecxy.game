using Vecxy.Assets;

namespace HardCore.Cultivation;

public sealed class MissionsConfig : IYamlConfig
{
    public List<MissionInfo> Missions { get; set; } = [];
}

public sealed class MissionInfo
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int DurationTicks { get; set; } = 10;
    public double RequiredCombatPower { get; set; }
    public ECultivationStage RequiredStage { get; set; }
    public int RequiredRealmLevel { get; set; } = 1;
    public int RequiredReputation { get; set; }
    public List<ItemStack> Costs { get; set; } = [];
    public List<ItemStack> Rewards { get; set; } = [];
    public List<StatModifier> StatRewards { get; set; } = [];
}
