using Vecxy.Assets;
using Vecxy.Engine;
using Vecxy.Kernel;
using HardCore.Cultivation;
using Vecxy.Editor;

public static class Program
{
    public static void Main(string[] args)
    {
        var options = new Engine.Options
        {
            Headless = false,
            Window = new IWindow.Options("HardCore Cultivation", 450, 900)
        };

        var assetsOptions = new AssetsModule.Options
        {
            AssetsDirectory = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "./Assets"))
        };

        var layers = new List<AAppLayer.ADefinition>
        {
            new EngineLayer.Definition(assetsOptions),
            new EditorLayer.Definition(),
            new GameLayer.Definition()
        };

        using var engine = new Engine(options, layers);

        engine.Run();
    }
}
