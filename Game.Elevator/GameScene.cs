using System.Numerics;
using Vecxy.Rendering;
using Vecxy.Scene;

namespace Game.Elevator;

public class Test : AComponent
{
    public override void Update(float deltaTime)
    {
     Transform.LocalRotation = Quaternion.Normalize(
         Quaternion.CreateFromAxisAngle(Vector3.UnitY, 0.5f * deltaTime) *
         Transform.LocalRotation);
    }
}

public class GameScene : IScene
{
    public void OnLoad(SceneInstance scene)
    {
        var cameraObject = scene.CreateObject("Camera");
        
        var camera = cameraObject.AddComponent<Camera>();
        var test = cameraObject.AddComponent<Test>();
    }
}