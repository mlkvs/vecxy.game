using Autofac;
using Vecxy.Assets;
using Vecxy.Engine;
using Vecxy.Kernel;
using Vecxy.Platforms;
using Vecxy.Scene;
using Vecxy.Usd;

namespace DoNotRelease;

[App]
public sealed class Application : IVEntry
{
    public void OnConfigureEngine(PlatformContext context, Engine.Options options)
    {
        options.ShowSplashScreen = false;
        options.Window = new IWindow.Options("НЕ ВЫПУСКАТЬ", 1280, 720);
        options.TargetFrameRate = 60;
    }
    public void OnConfigureLayers(PlatformContext context, List<AAppLayer.IDefinition> layers)
    {
        layers.Add(new EngineLayer.Definition(new AssetsModule.Options { AssetsDirectory = context.AssetsDirectory, HotReloadEnabled = false }, additionalModules: [new UsdModule.Definition()]));
        layers.Add(new GameLayer.Definition());
    }
}
public sealed class GameLayer(ISceneManager scenes) : AAppLayer
{
    public sealed class Definition : ADefinition<GameLayer>
    {
        public override void RegisterGlobal(ContainerBuilder builder) => builder.RegisterType<CellScene>().AsSelf();
    }
    public override void OnInitialize() => scenes.LoadScene<CellScene>();
}
public static class Program
{
    public static void Main(string[] args) => PlatformRunner.RunDesktop<Application>();
}
