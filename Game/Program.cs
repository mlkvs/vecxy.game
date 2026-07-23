using Vecxy.Diagnostics;
using Vecxy.Engine;
using Vecxy.Kernel;

namespace Game;

internal static class Program
{
    public static void Main(string[] args)
    {
        var options = new Engine.Options
        {
            Window = new WindowOptions("Game", 800, 600),
            AssetsPath = Env.IsDev
                ? Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../Assets"))
                : Path.Combine(AppContext.BaseDirectory, "Assets")
        };

        var layers = new List<AppLayer.IDefinition>
        {
            new EngineLayer.Definition(),
            new GameLayer.Definition(),
        };

        using var engine = new Engine(options, layers);

        engine.Run();
    }
}