using Vecxy.Assets;
using Vecxy.Editor;
using Vecxy.Engine;
using Vecxy.Kernel;

namespace Game.Elevator;

public static class Program
{
    public static void Main(string[] args)
    {
        var options = new Engine.Options
        {
            Window = new IWindow.Options("Sandbox", 1280, 720, 1),
            TargetFrameRate = 60
        };

        var layers = new List<AAppLayer.IDefinition>
        {
            new EngineLayer.Definition(
                new AssetsModule.Options
                {
                    AssetsDirectory = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "./Assets"))
                }),
            new GameLayer.Definition()
        };

        using var engine = new Engine(options, layers);

        engine.Run();
    }
}