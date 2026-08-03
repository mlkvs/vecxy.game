using Vecxy.Assets;

namespace HardCore.Cultivation;

public sealed class WorldStats
{
    public sealed class Config : IYamlConfig
    {
        public int DefaultTickIntervalMs { get; set; } = 1000;
        public double AgeDaysPerTick { get; set; } = 0.01;
        public double BaseSpiritualPowerPerTick { get; set; } = 1.0;
        public double BaseCombatPowerPerTrainingTick { get; set; } = 1.0;
        public int AutoSaveEveryTicks { get; set; } = 30;
    }

    public int TickIntervalMs { get; private set; }
    public double AgeDaysPerTick { get; private set; }
    public double BaseSpiritualPowerPerTick { get; private set; }
    public double BaseCombatPowerPerTrainingTick { get; private set; }
    public int AutoSaveEveryTicks { get; private set; }

    public void Initialize(ConfigRef<Config> config)
    {
        TickIntervalMs = Math.Max(1, config.Value.DefaultTickIntervalMs);
        AgeDaysPerTick = Math.Max(0.0, config.Value.AgeDaysPerTick);
        BaseSpiritualPowerPerTick = Math.Max(0.0, config.Value.BaseSpiritualPowerPerTick);
        BaseCombatPowerPerTrainingTick = Math.Max(0.0, config.Value.BaseCombatPowerPerTrainingTick);
        AutoSaveEveryTicks = Math.Max(1, config.Value.AutoSaveEveryTicks);
    }
}
