using JetBrains.Annotations;
using Vecxy.Diagnostics;
using Vecxy.Engine;
using Vecxy.Scene;

namespace Game;

public class Cube : Component
{
    protected override void Start()
    {
        Logger.Debug("Cube is start!");
    }

    protected override void Update(float deltaTime)
    {
        Logger.Debug($"Cube is update! {deltaTime}");
    }
}

[UsedImplicitly]
public sealed class GameLayer(ISceneManager sceneManager) : AppLayer
{
    public sealed class Definition : Definition<GameLayer>;
    
    public override void OnInitialize()
    {
        Logger.Level = ELogLevel.Trace;
        
        var scene = new Scene("Main");

        var cubeObject = scene.CreateObject("Cube");

        var cube = cubeObject.AddComponent<Cube>();
        
        Logger.Debug($"Cube is create! {cube}");

        sceneManager.SetActiveScene(scene);
        
        Logger.Debug($"Scene is active! Name: {scene.Name}");
        
        cube.SceneObject.Destroy();
    }
}


