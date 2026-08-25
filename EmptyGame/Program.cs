using Vecxy.Assets;
using Vecxy.Engine;
using Vecxy.Kernel;
using Vecxy.Platforms;

namespace EmptyGame;

[VecxyApplication]
public sealed class Application : IEntryPoint
{
    public void OnConfigureEngine(PlatformContext context, Engine.Options options)
    {
        options.ShowSplashScreen = false;
        options.Window = new IWindow.Options("Vecxy Empty Game", 480, 800);
        options.TargetFrameRate = 60;
    }

    public void OnConfigureLayers(PlatformContext context, List<AAppLayer.IDefinition> layers)
    {
        layers.Add(new EngineLayer.Definition(new AssetsModule.Options
        {
            AssetsDirectory = context.AssetsDirectory
        }));
    }
}

#if !ANDROID
public static class Program
{
    public static void Main(string[] args) => PlatformRunner.RunDesktop<Application>();
}
#endif
