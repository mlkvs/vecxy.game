namespace HardCore.Cultivation;

public sealed class MissionSystem
(
    GameDatabase database,
    Inventory inventory,
    CharacterStats stats,
    PlayerProgress progress
)
{
    private readonly Dictionary<string, MissionRuntime> _runtime = new(StringComparer.Ordinal);

    public event Action<MissionRuntime>? MissionChanged;

    public IReadOnlyCollection<MissionRuntime> Missions => _runtime.Values;

    public void Initialize()
    {
        foreach (var mission in database.Missions.Values)
        {
            _runtime.TryAdd(mission.Id, new MissionRuntime
            {
                MissionId = mission.Id,
                State = EMissionState.Available
            });
        }
    }

    public bool IsUnlocked(MissionInfo mission)
    {
        if (stats.CombatPower < mission.RequiredCombatPower)
            return false;

        if (progress.Stage < mission.RequiredStage)
            return false;

        if (progress.Stage == mission.RequiredStage &&
            progress.RealmLevel < mission.RequiredRealmLevel)
            return false;

        return progress.SectReputation >= mission.RequiredReputation;
    }

    public bool TryStart(string missionId)
    {
        var info = database.GetMission(missionId);
        var runtime = GetRuntime(missionId);

        if (runtime.State is EMissionState.Running or EMissionState.Completed)
            return false;

        if (!IsUnlocked(info))
            return false;

        if (!inventory.TryRemove(info.Costs))
            return false;

        runtime.State = EMissionState.Running;
        runtime.RemainingTicks = Math.Max(1, info.DurationTicks);
        MissionChanged?.Invoke(runtime);
        return true;
    }

    public bool TryClaim(string missionId)
    {
        var info = database.GetMission(missionId);
        var runtime = GetRuntime(missionId);

        if (runtime.State != EMissionState.Completed)
            return false;

        inventory.Add(info.Rewards);

        foreach (var reward in info.StatRewards)
            stats.Add(reward.Stat, reward.Value);

        runtime.State = EMissionState.Available;
        runtime.RemainingTicks = 0;
        MissionChanged?.Invoke(runtime);
        return true;
    }

    public void Tick()
    {
        foreach (var runtime in _runtime.Values)
        {
            if (runtime.State != EMissionState.Running)
                continue;

            runtime.RemainingTicks--;

            if (runtime.RemainingTicks <= 0)
            {
                runtime.RemainingTicks = 0;
                runtime.State = EMissionState.Completed;
            }

            MissionChanged?.Invoke(runtime);
        }
    }

    public MissionRuntime GetRuntime(string missionId)
    {
        if (_runtime.TryGetValue(missionId, out var runtime))
            return runtime;

        throw new KeyNotFoundException($"Mission runtime not found: {missionId}");
    }

    public void ReplaceWith(IEnumerable<MissionRuntime> missions)
    {
        _runtime.Clear();

        foreach (var mission in missions)
            _runtime[mission.MissionId] = mission;

        Initialize();
    }
}
