using Autofac;
using Vecxy.Engine;
using Vecxy.Scene;

namespace Horror;

public sealed class HorrorLayer(ISceneManager scenes) : AAppLayer
{
    public sealed class Definition : ADefinition<HorrorLayer>
    {
        public override void RegisterGlobal(ContainerBuilder builder) =>
            builder.RegisterType<MainScene>().AsSelf();
    }

    public override void OnInitialize() => scenes.LoadScene<MainScene>();
}
