using System.Numerics;
using Autofac;
using JetBrains.Annotations;
using Vecxy.Assets;
using Vecxy.Engine;
using Vecxy.Input;
using Vecxy.Kernel;
using Vecxy.Rendering;
using Vecxy.Scene;

namespace Game.Elevator;

[UsedImplicitly]
public class GameLayer
(
    ISceneManager scenes, 
    IAssetsManager assets, 
    IInputManager input,
    IWindow window
) : AAppLayer
{
    public class Definition : ADefinition<GameLayer>
    {
        public override void RegisterGlobal(ContainerBuilder builder)
        {
            builder
                .RegisterType<GameScene>()
                .AsSelf();
        }
    }
    
    public override void OnInitialize()
    {
        base.OnInitialize();

        var scene = scenes.LoadScene<GameScene>();

        /*
        var scene = sceneFactory.Create();
        scene.Lighting.AmbientIntensity = 0.0f;

        var camera = scene.CreateObject("Main Camera").AddComponent<Camera>();

        var inputConfig = assets.Load<InputAsset>("Controls.input");
        var fly = camera.SceneObject!.AddComponent(new FlyCamera(input, inputConfig, window));

        scenes.SetActiveScene(scene);

        var sceneModel = assets.Load<ModelAsset>("Models/Scene.glb");

        var sceneObject = instantiator.InstantiateModel(scene,  new Model(sceneModel));

        foreach (var light in sceneObject.GetComponentsInChildren<ALight>())
            light.Enabled = false;

        var flashlight = camera.SceneObject.AddComponent<SpotLight>();
        flashlight.Color = new Vector3(1.0f, 0.92f, 0.78f);
        flashlight.Intensity = 5000.0f;
        flashlight.Range = 20.0f;
        flashlight.InnerConeAngle = MathF.PI / 9.0f;
        flashlight.OuterConeAngle = MathF.PI / 6.0f;*/
    }
}
