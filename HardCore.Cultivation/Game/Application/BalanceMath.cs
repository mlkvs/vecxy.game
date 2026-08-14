using HardCore.Cultivation.Game.Domain;
using HardCore.Cultivation.Game.Infrastructure;

namespace HardCore.Cultivation.Game.Application;

public readonly record struct CharacterStats(decimal MaximumHealth, decimal HealthRegeneration, decimal Attack,
    decimal AttacksPerSecond, decimal LongevityYears)
{
    public static CharacterStats operator +(CharacterStats left, CharacterStats right) => new(
        left.MaximumHealth + right.MaximumHealth, left.HealthRegeneration + right.HealthRegeneration,
        left.Attack + right.Attack, left.AttacksPerSecond + right.AttacksPerSecond,
        left.LongevityYears + right.LongevityYears);
    public static CharacterStats operator *(CharacterStats value, decimal scalar) => new(
        value.MaximumHealth * scalar, value.HealthRegeneration * scalar, value.Attack * scalar,
        value.AttacksPerSecond * scalar, value.LongevityYears * scalar);
}

public sealed class PiecewiseLinearCurve<TPoint>(IReadOnlyList<TPoint> points, Func<TPoint, decimal> x, Func<TPoint, decimal> y)
{
    private readonly TPoint[] _points = points.OrderBy(x).ToArray();
    public decimal Evaluate(decimal value)
    {
        if (_points.Length == 0) throw new ArgumentException("Curve is empty.");
        if (value <= x(_points[0])) return y(_points[0]);
        if (value >= x(_points[^1])) return y(_points[^1]);
        for (var i = 1; i < _points.Length; i++) if (value <= x(_points[i]))
        {
            var a = _points[i - 1]; var b = _points[i];
            return y(a) + (y(b) - y(a)) * (value - x(a)) / (x(b) - x(a));
        }
        throw new InvalidOperationException();
    }
}

public sealed class CultivationBalanceSnapshot
{
    private readonly decimal[,] _costs;
    private readonly CharacterStats[] _starts;
    private readonly CharacterStats[] _ends;
    public CultivationBalanceSnapshot(GameBalanceConfig balance, CultivationConfig config)
    {
        _costs = new decimal[config.Stages.Count, 10]; _starts = new CharacterStats[config.Stages.Count]; _ends = new CharacterStats[config.Stages.Count];
        _starts[0] = new(balance.MaximumAgeYears * 0m + 100m, 1m, 1m, 1m, balance.MaximumAgeYears);
        for (var s = 0; s < config.Stages.Count; s++)
        {
            if (s == 0) { _costs[s, 0] = config.InitialRequiredPower[0]; _costs[s, 1] = config.InitialRequiredPower[1]; }
            else { _costs[s, 0] = config.StageEntryCoefficient * (_costs[s - 1, 9] + _costs[s - 1, 8]); _costs[s, 1] = config.StageEntryCoefficient * (_costs[s, 0] + _costs[s - 1, 9]); }
            for (var l = 2; l < 10; l++) _costs[s, l] = config.Stages[s].RecursiveCoefficient * (_costs[s, l - 1] + _costs[s, l - 2]);
            _ends[s] = _starts[s] + config.Stages[s].StatsPerLevel * 10m;
            if (s + 1 < config.Stages.Count) _starts[s + 1] = _ends[s] + config.Stages[s].BreakthroughBonus;
        }
    }
    public decimal GetCost(int stage, int level) => _costs[stage, level - 1];
    public CharacterStats GetStart(int stage) => _starts[stage];
    public CharacterStats GetEnd(int stage) => _ends[stage];
    public CharacterStats GetCurrent(CultivationProgress cultivation, CultivationConfig config) =>
        _starts[cultivation.StageIndex] + config.Stages[cultivation.StageIndex].StatsPerLevel * (cultivation.Level - 1);
}

public static class ElementCompatibilityCalculator
{
    public static decimal GetModifier(IEnumerable<Element?> elements, AlchemyConfig config)
    {
        var values = elements.Where(value => value.HasValue).Select(value => value!.Value).ToArray(); var sum = 0m;
        for (var i = 0; i < values.Length; i++) for (var j = i + 1; j < values.Length; j++) sum += config.ElementCompatibility[values[i]][values[j]];
        return 1m + config.ElementCompatibilityCoefficient * sum;
    }
}

public static class ContaminationCalculator
{
    public static ContaminationLevelConfig? GetLevel(decimal contamination, GameBalanceConfig balance) =>
        balance.ContaminationLevels
            .Where(level => contamination >= level.MinimumContamination)
            .OrderByDescending(level => level.MinimumContamination)
            .FirstOrDefault();

    public static IReadOnlyList<ItemEffectDefinition> GetEffects(decimal contamination, GameBalanceConfig balance) =>
        GetLevel(contamination, balance)?.Effects ?? [];
}
