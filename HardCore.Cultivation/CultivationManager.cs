using Vecxy.Diagnostics;
using Vecxy.UI;

namespace HardCore.Cultivation;

public sealed class CultivationManager
(
    IUiManager ui,
    WorldStats worldStats,
    CharacterStats characterStats,
    CultivationSystem cultivation,
    MissionSystem missions,
    CraftingSystem crafting,
    GameSaveSystem saves
)
{
    private UiDocument? _main;
    private float _elapsedMs;

    public event Action? TickCompleted;

    public CharacterStats Character => characterStats;
    public CultivationSystem Cultivation => cultivation;
    public MissionSystem Missions => missions;
    public CraftingSystem Crafting => crafting;

    public void Initialize()
    {
        _main = ui.Load("UI/Main.xml");

        missions.Initialize();

        if (!saves.TryLoad())
            InitializeNewGame();
    }

    public void Update(float deltaTime)
    {
        _elapsedMs += deltaTime * 1000f;

        while (_elapsedMs >= worldStats.TickIntervalMs)
        {
            _elapsedMs -= worldStats.TickIntervalMs;
            Tick();
        }
    }

    public void Save()
    {
        saves.Save();
    }

    private void Tick()
    {
        cultivation.Tick();
        missions.Tick();
        crafting.Tick();

        saves.TotalTicks++;

        if (saves.TotalTicks % worldStats.AutoSaveEveryTicks == 0)
            saves.Save();

        TickCompleted?.Invoke();

        Logger.Info(
            $"Tick {saves.TotalTicks}: Spirit={characterStats.SpiritualPower:0.##}, Combat={characterStats.CombatPower:0.##}");
    }

    private void InitializeNewGame()
    {
        characterStats.SpiritualPower = 0.0;
        characterStats.CombatPower = 1.0;
        characterStats.Health = 100.0;
        characterStats.MaxHealth = 100.0;
        characterStats.Money = 0.0;
        characterStats.LifespanDays = 3650.0;
        characterStats.AgeDays = 0.0;
        characterStats.SpiritualPowerPerTick = 0.0;
        characterStats.CombatPowerPerTick = 0.0;
        characterStats.MoneyPerTick = 0.0;
    }
}
