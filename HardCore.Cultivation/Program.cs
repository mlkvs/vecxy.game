using Vecxy.Assets;
using Vecxy.Engine;
using Vecxy.Kernel;
using Vecxy.Platforms;

namespace HardCore.Cultivation;

[VecxyApplication]
public sealed class Application : IEntryPoint
{
    public void OnConfigureEngine(PlatformContext context, Engine.Options options)
    {
        options.ShowSplashScreen = true;
        options.Window = new IWindow.Options("HardCore Cultivation", 500, 900);
        options.TargetFrameRate = 60;
    }

    public void OnConfigureLayers(PlatformContext context, List<AAppLayer.IDefinition> layers)
    {
        layers.Add(new EngineLayer.Definition(new AssetsModule.Options
        {
            AssetsDirectory = context.AssetsDirectory
        }));
        
        layers.Add(new GameLayer.Definition());
        layers.Add(new CheatLayer.Definition());
        
#if !ANDROID
        // layers.Add(new UiProfilerLayer.Definition());
#endif
    }
}

#if !ANDROID
public static class Program
{
    public static void Main(string[] args)
    {
        PlatformRunner.RunDesktop<Application>();
    }
}
#endif
