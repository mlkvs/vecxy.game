namespace HardCore.Cultivation;

public sealed class CultivationSystem
(
    GameDatabase database,
    CharacterStats stats,
    PlayerProgress progress,
    WorldStats world
)
{
    public bool IsMeditating { get; private set; } = true;
    public bool IsTraining { get; private set; }

    public event Action? ProgressChanged;
    public event Action<ECultivationStage>? StageChanged;

    public void SetMeditating(bool value)
    {
        IsMeditating = value;
    }

    public void SetTraining(bool value)
    {
        IsTraining = value;
    }

    public void Tick()
    {
        if (IsMeditating)
        {
            stats.SpiritualPower +=
                world.BaseSpiritualPowerPerTick +
                stats.SpiritualPowerPerTick;
        }

        if (IsTraining)
        {
            stats.CombatPower +=
                world.BaseCombatPowerPerTrainingTick +
                stats.CombatPowerPerTick;
        }

        stats.Money += stats.MoneyPerTick;
        stats.AgeDays += world.AgeDaysPerTick;
    }

    public double GetNextLevelCost()
    {
        var stage = database.GetStage(progress.Stage);
        return stage.BaseSpiritualPowerCost *
               Math.Pow(stage.LevelCostMultiplier, progress.RealmLevel - 1);
    }

    public bool CanAdvanceLevel()
    {
        return progress.RealmLevel < database.LevelsPerStage &&
               stats.SpiritualPower >= GetNextLevelCost();
    }

    public bool TryAdvanceLevel()
    {
        if (!CanAdvanceLevel())
            return false;

        stats.SpiritualPower -= GetNextLevelCost();
        progress.RealmLevel++;
        ProgressChanged?.Invoke();
        return true;
    }

    public bool CanBreakthrough()
    {
        if (progress.RealmLevel < database.LevelsPerStage)
            return false;

        if (progress.Stage == Enum.GetValues<ECultivationStage>().Max())
            return false;

        var stage = database.GetStage(progress.Stage);
        return stats.SpiritualPower >= stage.BreakthroughCost;
    }

    public bool TryBreakthrough()
    {
        if (!CanBreakthrough())
            return false;

        var current = database.GetStage(progress.Stage);
        stats.SpiritualPower -= current.BreakthroughCost;

        progress.Stage++;
        progress.RealmLevel = 1;

        var next = database.GetStage(progress.Stage);
        stats.SpiritualPowerPerTick += next.SpiritualPowerPerTickBonus;
        stats.CombatPower += next.CombatPowerBonus;
        stats.LifespanDays += next.LifespanDaysBonus;

        StageChanged?.Invoke(progress.Stage);
        ProgressChanged?.Invoke();
        return true;
    }
}
