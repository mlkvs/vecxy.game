using Autofac;
using JetBrains.Annotations;
using Vecxy.Engine;
using Vecxy.Kernel;
using Vecxy.Scene;

namespace AnimationGame;

[UsedImplicitly]
[Layer("animation-demo")]
public sealed class GameLayer(ISceneManager scenes) : AAppLayer
{
    public sealed class Definition : ADefinition<GameLayer>
    {
        public override void RegisterGlobal(ContainerBuilder builder)
        {
            builder.RegisterType<AnimationDemoScene>().AsSelf();
        }
    }

    public override void OnInitialize() => scenes.LoadScene<AnimationDemoScene>();
    public override void OnUnload() => scenes.UnloadScene<AnimationDemoScene>();
}
