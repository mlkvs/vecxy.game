using Vecxy.Assets;
using Vecxy.Engine;
using Vecxy.Kernel;
using Vecxy.Platforms;
using Vecxy.Usd;

namespace Horror;

[App]
public sealed class Application : IVEntry
{
    public void OnConfigureEngine(PlatformContext context, Engine.Options options)
    {
        options.ShowSplashScreen = false;
        options.Window = new IWindow.Options("Horror", 1280, 720);
        options.TargetFrameRate = 60;
    }

    public void OnConfigureLayers(PlatformContext context, List<AAppLayer.IDefinition> layers)
    {
        layers.Add(new EngineLayer.Definition(new AssetsModule.Options
        {
            AssetsDirectory = context.AssetsDirectory
        }, additionalModules: [new UsdModule.Definition()]));
        layers.Add(new HorrorLayer.Definition());
    }
}

#if !ANDROID
public static class Program
{
    public static void Main(string[] args) => PlatformRunner.RunDesktop<Application>();
}
#endif
