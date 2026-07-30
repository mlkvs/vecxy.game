using Autofac;
using JetBrains.Annotations;
using Vecxy.Diagnostics;
using Vecxy.Engine;
using Vecxy.Physics;

namespace Game.Elevator;

[UsedImplicitly]
public class GameLayer(IPhysicsSystem physics) : AAppLayer
{
    public class Definition : ADefinition<GameLayer>
    {
        public override void RegisterLocal(ContainerBuilder builder)
        {
        }
    }
    
    public override void OnInitialize()
    {
        base.OnInitialize();
        
        Logger.Info(physics.Settings.Gravity.Y.ToString());
    }
}