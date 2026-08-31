using HardCore.Cultivation.Game.Infrastructure;

namespace HardCore.Cultivation.Game.Application;

public sealed class TapComboTracker
{
    private readonly TapComboConfig _config;
    private readonly TapComboLevelConfig[] _levels;
    private float _secondsSinceTap;
    private bool _firstLevelUnlocked;

    public TapComboTracker(TapComboConfig config)
    {
        _config = config;
        _levels = config.Levels.OrderBy(level => level.MinimumCombo).ToArray();
    }

    public float Value { get; private set; }
    public bool IsActive => _config.Enabled && _firstLevelUnlocked && Value > 0f && _levels.Length > 0;
    public int DisplayCount => Math.Max(0, LevelIndex + 1);
    public int LevelIndex
    {
        get
        {
            if (!IsActive)
                return -1;
            // Once x1 has been earned, keep showing it while the remaining combo drains to zero.
            var index = 0;
            for (var candidate = 1; candidate < _levels.Length; candidate++)
            {
                if (Value < _levels[candidate].MinimumCombo)
                    break;
                index = candidate;
            }
            return index;
        }
    }

    public TapComboLevelConfig? CurrentLevel => LevelIndex is var index && index >= 0 ? _levels[index] : null;
    public decimal PowerMultiplier => CurrentLevel?.PowerMultiplier ?? 1m;
    public float RetentionProgress => !IsActive || _config.GracePeriodSeconds <= 0f
        ? 0f
        : Math.Clamp(1f - _secondsSinceTap / _config.GracePeriodSeconds, 0f, 1f);

    public bool RegisterTap()
    {
        if (!_config.Enabled || _levels.Length == 0)
            return false;
        var before = Snapshot();
        Value = Math.Min(_config.MaximumCombo, Value + _config.PointsPerTap);
        if (Value >= _levels[0].MinimumCombo)
            _firstLevelUnlocked = true;
        _secondsSinceTap = 0f;
        return before != Snapshot();
    }

    public bool Update(float deltaTime)
    {
        if (!_config.Enabled || Value <= 0f || _levels.Length == 0 || deltaTime <= 0f)
            return false;
        var before = Snapshot();
        _secondsSinceTap += deltaTime;
        if (_secondsSinceTap >= _config.GracePeriodSeconds)
        {
            Value = 0f;
            _firstLevelUnlocked = false;
            _secondsSinceTap = 0f;
        }
        return before != Snapshot();
    }

    public bool Reset()
    {
        if (Value <= 0f && _secondsSinceTap <= 0f)
            return false;
        Value = 0f;
        _secondsSinceTap = 0f;
        _firstLevelUnlocked = false;
        return true;
    }

    private (int Count, int Level, int Retention) Snapshot() =>
        (DisplayCount, LevelIndex, (int)MathF.Round(RetentionProgress * 100f));
}
