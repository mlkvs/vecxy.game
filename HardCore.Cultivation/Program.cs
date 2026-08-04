using Vecxy.Assets;
using Vecxy.Engine;
using Vecxy.Kernel;
using Vecxy.Platforms;
#if !ANDROID
using Vecxy.Editor;
#endif

namespace HardCore.Cultivation;

[VecxyApplication]
public sealed class CultivationApplication : IVecxyApplication
{
    public Engine.Options CreateEngineOptions(PlatformContext context) => new()
    {
        Headless = false,
        TargetFrameRate = 60,
        ShowSplashScreen = true,
        Window = new IWindow.Options("HardCore Cultivation", 450, 900)
    };

    public IReadOnlyList<AAppLayer.IDefinition> CreateLayers(PlatformContext context)
    {
        var layers = new List<AAppLayer.IDefinition>
        {
            new EngineLayer.Definition(new AssetsModule.Options
            {
                AssetsDirectory = context.AssetsDirectory
            })
        };
#if !ANDROID
        layers.Add(new EditorLayer.Definition());
#endif
        layers.Add(new GameLayer.Definition());
        return layers;
    }
}

#if !ANDROID
public static class Program
{
    public static void Main(string[] args)
    {
        PlatformRunner.RunDesktop<CultivationApplication>();
    }
}
#endif
