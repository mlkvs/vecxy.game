using System.Text.Json;

namespace HardCore.Cultivation.Progression;

public enum ActivityKind { None, Meditation, Mission }

public sealed class GameProgress
{
    public double Qi { get; set; } = 21.2;
    public int SpiritStones { get; set; } = 240;
    public int RealmIndex { get; set; }
    public int RealmLevel { get; set; } = 1;
    public double CombatPower { get; set; } = 1.4;
    public double Age { get; set; } = 15;
    public int BodyLevel { get; set; }
    public int TechniqueLevel { get; set; }
    public ActivityKind Activity { get; set; } = ActivityKind.Meditation;
    public string ActiveMissionId { get; set; } = string.Empty;
    public double ActivityRemaining { get; set; }
    public long LastSavedUtcTicks { get; set; } = DateTime.UtcNow.Ticks;
    public Dictionary<string, int> Inventory { get; set; } = new(StringComparer.Ordinal);
    public HashSet<string> Pets { get; set; } = [];

    public static readonly string[] Realms =
    [
        "Закалка тела", "Сбор Ци", "Основание Фундамента", "Золотое Ядро",
        "Зарождающаяся Душа", "Преображение Духа", "Вознесение"
    ];

    public double QiRequired => 30 * Math.Pow(2.15, RealmIndex) * (1 + (RealmLevel - 1) * .35);
    public string RealmName => Realms[Math.Clamp(RealmIndex, 0, Realms.Length - 1)];
    public double QiPerSecond => .1 * (1 + BodyLevel * .12 + TechniqueLevel * .18 + Pets.Count * .1);
    public double EffectiveCombatPower => CombatPower + RealmIndex * 75 + RealmLevel * 8 + BodyLevel * 5 + TechniqueLevel * 7 + Pets.Count * 12;

    public void AddQi(double amount)
    {
        Qi = Math.Min(QiRequired, Qi + Math.Max(0, amount));
    }

    public bool Breakthrough()
    {
        if (Qi + .0001 < QiRequired) return false;
        Qi = 0;
        RealmLevel++;
        if (RealmLevel > 10)
        {
            RealmLevel = 1;
            RealmIndex = Math.Min(RealmIndex + 1, Realms.Length - 1);
        }
        CombatPower += 9 + RealmIndex * 5;
        return true;
    }
}

public sealed record MissionDefinition(string Id, string Name, string Category, double Duration, double RequiredPower, int Stones, string? ItemId = null, int ItemQuantity = 0);

public static class GameContent
{
    public static readonly MissionDefinition[] Missions =
    [
        new("herbs", "Собрать духовные травы", "Сбор", 8, 1, 18, "spirit-herb", 2),
        new("patrol", "Патруль у ворот секты", "Секта", 16, 10, 38),
        new("wolves", "Отогнать небесных волков", "Охота", 28, 28, 75, "blood-elixir", 2),
        new("bandits", "Победить горных бандитов", "Бой", 45, 60, 130, "azure-dagger", 1),
        new("cave", "Исследовать древнюю пещеру", "Поиск", 70, 120, 280, "spirit-ring", 1),
        new("demon", "Сразить демона алого ядра", "Элита", 120, 300, 750, "flame-blade", 1)
    ];
}

public static class SaveGame
{
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };
    public static string Path => System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Vecxy", "HardCore.Cultivation", "save.json");

    public static GameProgress Load()
    {
        try
        {
            if (File.Exists(Path))
            {
                var progress = JsonSerializer.Deserialize<GameProgress>(File.ReadAllText(Path), Options) ?? new GameProgress();
                progress.RealmIndex = Math.Clamp(progress.RealmIndex, 0, GameProgress.Realms.Length - 1);
                progress.RealmLevel = Math.Clamp(progress.RealmLevel, 1, 10);
                progress.Qi = Math.Clamp(progress.Qi, 0, progress.QiRequired);
                progress.SpiritStones = Math.Max(0, progress.SpiritStones);
                progress.BodyLevel = Math.Max(0, progress.BodyLevel);
                progress.TechniqueLevel = Math.Max(0, progress.TechniqueLevel);
                progress.Inventory ??= new Dictionary<string, int>(StringComparer.Ordinal);
                progress.Pets ??= [];
                return progress;
            }
        }
        catch { /* Corrupt saves start safely from defaults. */ }
        return new GameProgress();
    }

    public static void Save(GameProgress progress)
    {
        progress.LastSavedUtcTicks = DateTime.UtcNow.Ticks;
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(Path)!);
        File.WriteAllText(Path, JsonSerializer.Serialize(progress, Options));
    }
}
