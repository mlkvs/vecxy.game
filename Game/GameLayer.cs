using JetBrains.Annotations;
using Vecxy.Diagnostics;
using Vecxy.Engine;

namespace Game;

[UsedImplicitly]
public sealed class GameLayer : AppLayer
{
    public sealed class Definition : Definition<GameLayer>;
    
    public override void OnInitialize()
    {
    }
}


