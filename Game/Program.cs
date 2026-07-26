using Vecxy.Assets;
using Vecxy.Editor;
using Vecxy.Engine;
using Vecxy.Kernel;

namespace Game;

internal static class Program
{
    public static void Main(string[] args)
    {
        var options = new Engine.Options
        {
            Window = new IWindow.Options("Game", 800, 600),
            TargetFrameRate = 60
        };

        var layers = new List<AAppLayer.IDefinition>
        {
            new EngineLayer.Definition(
                new AssetsModule.Options
                {
                    AssetsDirectory = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "Assets"))
                }),
            new EditorLayer.Definition(),
            new GameLayer.Definition()
        };

        using var engine = new Engine(options, layers);

        engine.Run();
    }
}
