using JetBrains.Annotations;
using Mediator.Net;
using Vecxy.Diagnostics;
using Vecxy.Engine;
using Vecxy.Kernel;
using Vecxy.Platforms;

PlatformRunner.RunDesktop<Application>();

[UsedImplicitly]
internal class Application : IVecxyApplication
{
    public Engine.Options CreateEngineOptions(PlatformContext context)
    {
        return new Engine.Options
        {
            Headless = false,
            ShowSplashScreen = false,
            TargetFrameRate = 60,
            Window = new IWindow.Options("Sandbox.Tests", 800, 600, 1)
        };
    }

    public IReadOnlyList<AAppLayer.IDefinition> CreateLayers(PlatformContext context)
    {
        return new List<AAppLayer.IDefinition>
        {
            new EngineLayer.Definition(),
            new Layer.Definition()
        };
    }
    
    [UsedImplicitly]
    public class Layer(IMediator mediator) : AAppLayer
    {
        public class Definition : ADefinition<Layer>;

        public override void OnInitialize()
        {
            
        }
    }
}