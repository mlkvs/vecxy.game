using Autofac;
using Vecxy.Engine;
using Vecxy.Scene;

namespace Sponza;

public sealed class SponzaLayer(ISceneManager scenes) : AAppLayer
{
    public sealed class Definition : ADefinition<SponzaLayer>
    {
        public override void RegisterGlobal(ContainerBuilder builder) =>
            builder.RegisterType<MainScene>().AsSelf();
    }

    public override void OnInitialize() => scenes.LoadScene<MainScene>();
}
