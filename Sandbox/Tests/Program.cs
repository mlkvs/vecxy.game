using Autofac;
using JetBrains.Annotations;
using Vecxy.Engine;
using Vecxy.Kernel;
using Vecxy.Platforms;
using Vecxy.Rendering;
using Vecxy.Scene;

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
    public class Layer(ISceneManager scenes) : AAppLayer
    {
        public class Definition : ADefinition<Layer>
        {
            public override void RegisterGlobal(ContainerBuilder builder)
            {
                builder.RegisterType<Boot>().AsSelf();
            }
        }

        public override void OnInitialize()
        {
            scenes.LoadScene<Boot>();
        }
    }

    public class Boot(IComponentInstantiator instantiator) : IScene
    {
        public void OnLoad(SceneInstance scene)
        {
            scene.Lighting.Skybox.Enabled = true;

            instantiator.Instantiate<Camera>(scene);
        }
    }
}