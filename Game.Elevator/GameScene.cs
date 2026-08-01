using Game.Elevator.InteractiveMap;
using Vecxy.Assets;
using Vecxy.Input;
using Vecxy.Kernel;
using Vecxy.Rendering;
using Vecxy.Scene;

namespace Game.Elevator;

public sealed class GameScene(
    IAssetsManager assets,
    IRenderer renderer,
    IInputManager input,
    IWindow window) : IScene
{
    public IInteractiveMap? Map { get; private set; }

    public void OnLoad(SceneInstance scene)
    {
        var mapObject = scene.CreateObject("Interactive Map");
        Map = mapObject.AddComponent(
            new InteractiveMap.InteractiveMap(
                assets,
                renderer,
                input,
                window));
    }

    public void OnUnload(SceneInstance scene)
    {
        Map = null;
    }
}
