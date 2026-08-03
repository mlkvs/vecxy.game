using Vecxy.Assets;

namespace HardCore.Cultivation;

public sealed class CultivationConfig : IYamlConfig
{
    public int LevelsPerStage { get; set; } = 9;
    public List<CultivationStageInfo> Stages { get; set; } = [];
}

public sealed class CultivationStageInfo
{
    public ECultivationStage Stage { get; set; }
    public string Name { get; set; } = string.Empty;
    public double BaseSpiritualPowerCost { get; set; } = 100.0;
    public double LevelCostMultiplier { get; set; } = 1.5;
    public double BreakthroughCost { get; set; } = 1000.0;
    public double SpiritualPowerPerTickBonus { get; set; }
    public double CombatPowerBonus { get; set; }
    public double LifespanDaysBonus { get; set; }
}
