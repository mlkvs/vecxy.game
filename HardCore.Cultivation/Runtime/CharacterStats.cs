namespace HardCore.Cultivation;

public sealed class CharacterStats
{
    public double SpiritualPower { get; set; }
    public double CombatPower { get; set; }
    public double Health { get; set; } = 100.0;
    public double MaxHealth { get; set; } = 100.0;
    public double Money { get; set; }
    public double LifespanDays { get; set; } = 3650.0;
    public double AgeDays { get; set; }

    public double SpiritualPowerPerTick { get; set; } = 1.0;
    public double CombatPowerPerTick { get; set; }
    public double MoneyPerTick { get; set; }

    public void Add(EStatType stat, double value)
    {
        switch (stat)
        {
            case EStatType.SpiritualPower:
                SpiritualPower = Math.Max(0.0, SpiritualPower + value);
                break;
            case EStatType.CombatPower:
                CombatPower = Math.Max(0.0, CombatPower + value);
                break;
            case EStatType.Health:
                Health = Math.Clamp(Health + value, 0.0, MaxHealth);
                break;
            case EStatType.Lifespan:
                LifespanDays = Math.Max(0.0, LifespanDays + value);
                break;
            case EStatType.Money:
                Money = Math.Max(0.0, Money + value);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(stat), stat, null);
        }
    }

    public bool TrySpend(EStatType stat, double value)
    {
        if (value < 0.0)
            throw new ArgumentOutOfRangeException(nameof(value));

        var current = Get(stat);
        if (current < value)
            return false;

        Add(stat, -value);
        return true;
    }

    public double Get(EStatType stat)
    {
        return stat switch
        {
            EStatType.SpiritualPower => SpiritualPower,
            EStatType.CombatPower => CombatPower,
            EStatType.Health => Health,
            EStatType.Lifespan => LifespanDays,
            EStatType.Money => Money,
            _ => throw new ArgumentOutOfRangeException(nameof(stat), stat, null)
        };
    }
}
