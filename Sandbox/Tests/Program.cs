using Autofac;
using JetBrains.Annotations;
using Vecxy.Engine;
using Vecxy.Platforms;
using Vecxy.Rendering;
using Vecxy.Scene;

PlatformRunner.RunDesktop<Application>();

[UsedImplicitly]
internal class Application : IEntryPoint
{
    public void OnConfigureEngine(PlatformContext context, Engine.Options options)
    {
        options.TargetFrameRate = 60;
    }

    public void OnConfigureLayers(PlatformContext context, List<AAppLayer.IDefinition> layers)
    {
        layers.Add(new EngineLayer.Definition());
        layers.Add(new Layer.Definition());
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
}

[UsedImplicitly]
public class Boot(IComponentInstantiator instantiator) : IScene
{
    public void OnLoad(SceneInstance scene)
    {
        scene.Lighting.Skybox.Enabled = true;

        instantiator.Instantiate<Camera>(scene);
    }
}