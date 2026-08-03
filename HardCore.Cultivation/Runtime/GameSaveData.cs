namespace HardCore.Cultivation;

public sealed class GameSaveData
{
    public CharacterStats Character { get; set; } = new();
    public PlayerProgress Progress { get; set; } = new();
    public Dictionary<string, int> Inventory { get; set; } = new(StringComparer.Ordinal);
    public List<MissionRuntime> Missions { get; set; } = [];
    public List<CraftingRuntime> CraftingQueue { get; set; } = [];
    public long TotalTicks { get; set; }
}
