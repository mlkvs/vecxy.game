using System.Text.Json;

namespace HardCore.Cultivation;

public sealed class GameSaveSystem
(
    CharacterStats stats,
    PlayerProgress progress,
    Inventory inventory,
    MissionSystem missions,
    CraftingSystem crafting
)
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    public string SavePath { get; set; } =
        Path.Combine(AppContext.BaseDirectory, "cultivation-save.json");

    public long TotalTicks { get; set; }

    public void Save()
    {
        var data = new GameSaveData
        {
            Character = stats,
            Progress = progress,
            Inventory = inventory.Items.ToDictionary(x => x.Key, x => x.Value),
            Missions = missions.Missions.Select(CloneMission).ToList(),
            CraftingQueue = crafting.Queue.Select(CloneCrafting).ToList(),
            TotalTicks = TotalTicks
        };

        var directory = Path.GetDirectoryName(SavePath);

        if (!string.IsNullOrWhiteSpace(directory))
            Directory.CreateDirectory(directory);

        File.WriteAllText(SavePath, JsonSerializer.Serialize(data, JsonOptions));
    }

    public bool TryLoad()
    {
        if (!File.Exists(SavePath))
            return false;

        var json = File.ReadAllText(SavePath);
        var data = JsonSerializer.Deserialize<GameSaveData>(json, JsonOptions);

        if (data is null)
            return false;

        CopyCharacter(data.Character, stats);
        CopyProgress(data.Progress, progress);
        inventory.ReplaceWith(data.Inventory);
        missions.ReplaceWith(data.Missions);
        crafting.ReplaceWith(data.CraftingQueue);
        TotalTicks = data.TotalTicks;

        return true;
    }

    private static MissionRuntime CloneMission(MissionRuntime value)
    {
        return new MissionRuntime
        {
            MissionId = value.MissionId,
            State = value.State,
            RemainingTicks = value.RemainingTicks
        };
    }

    private static CraftingRuntime CloneCrafting(CraftingRuntime value)
    {
        return new CraftingRuntime
        {
            RecipeId = value.RecipeId,
            RemainingTicks = value.RemainingTicks,
            Amount = value.Amount
        };
    }

    private static void CopyCharacter(CharacterStats source, CharacterStats target)
    {
        target.SpiritualPower = source.SpiritualPower;
        target.CombatPower = source.CombatPower;
        target.Health = source.Health;
        target.MaxHealth = source.MaxHealth;
        target.Money = source.Money;
        target.LifespanDays = source.LifespanDays;
        target.AgeDays = source.AgeDays;
        target.SpiritualPowerPerTick = source.SpiritualPowerPerTick;
        target.CombatPowerPerTick = source.CombatPowerPerTick;
        target.MoneyPerTick = source.MoneyPerTick;
    }

    private static void CopyProgress(PlayerProgress source, PlayerProgress target)
    {
        target.Stage = source.Stage;
        target.RealmLevel = source.RealmLevel;
        target.SectReputation = source.SectReputation;
        target.AlchemyLevel = source.AlchemyLevel;
        target.AlchemyExperience = source.AlchemyExperience;
    }
}
