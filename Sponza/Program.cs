using Vecxy.Assets;
using Vecxy.Editor;
using Vecxy.Engine;
using Vecxy.Kernel;
using Vecxy.Platforms;

namespace Sponza;

[App]
public sealed class Application : IVEntry
{
    public void OnConfigureEngine(PlatformContext context, Engine.Options options)
    {
        options.ShowSplashScreen = false;
        options.Window = new IWindow.Options("Sponza", 1600, 900);
        options.TargetFrameRate = 60;
    }

    public void OnConfigureLayers(PlatformContext context, List<AAppLayer.IDefinition> layers)
    {
        layers.Add(new EngineLayer.Definition(new AssetsModule.Options
        {
            AssetsDirectory = context.AssetsDirectory
        }));
        layers.Add(new EditorLayer.Definition());
        layers.Add(new SponzaLayer.Definition());
    }
}

#if !ANDROID
public static class Program
{
    public static void Main(string[] args) => PlatformRunner.RunDesktop<Application>();
}
#endif
