using System.Numerics;
using Vecxy.Assets;
using Vecxy.Audio;
using Vecxy.Input;
using Vecxy.Physics;
using Vecxy.Prototypes;
using Vecxy.Rendering;
using Vecxy.Scene;
using Vecxy.Usd;
using Vecxy.UI;

namespace Horror;

public sealed class MainScene(
    IInputManager input,
    IAssetsManager assets,
    ISceneInstantiator sceneInstantiator,
    IPhysicsSystem physics,
    IAudioManager audio,
    IUiManager ui,
    IPrototypes prototypes) : IScene
{
    private AssetRef<InputAsset>? _controlsAsset;
    private InputMap? _playerInput;

    public void OnLoad(SceneInstance scene)
    {
        _controlsAsset = assets.Load<InputAsset>("Controls.input");
        _playerInput = input.Create(_controlsAsset, "Player");
        _playerInput.Enable();

        scene.Lighting.Skybox.Enabled = false;
        scene.Lighting.Exposure = 1.0f;
        scene.Lighting.AmbientSkyColor = new Vector3(0.42f, 0.48f, 0.56f);
        scene.Lighting.AmbientGroundColor = new Vector3(0.17f, 0.19f, 0.22f);
        scene.Lighting.AmbientIntensity = 1.0f;
        scene.Lighting.SpecularStrength = 0.16f;
        scene.Lighting.Fog.Color = new Vector3(0.16f, 0.20f, 0.25f);
        scene.Lighting.Fog.StartDistance = 18.0f;
        scene.Lighting.Fog.EndDistance = 42.0f;
        scene.Lighting.Fog.HeightEnabled = false;
        scene.Lighting.Fog.VolumetricStrength = 0.0f;

        var location = prototypes.Instantiate<UsdScene>(
            "Prototypes/Sandbox.prototype",
            new ScenePrototypeContext { Scene = scene });
        var usd = location.Instance;
        var spawn = usd.RequireRole("spawn");

        var cameraObject = scene.CreateObject("First Person Camera");
        var camera = cameraObject.AddComponent<Camera>();
        camera.ClearColor = new Vector4(0.16f, 0.20f, 0.25f, 1.0f);
        camera.FieldOfView = 75.0f;
        camera.NearPlane = 0.05f;
        camera.FarPlane = 90.0f;
        camera.UsePostProcessing = false;

        prototypes.Instantiate<Player>(
            new ScenePrototypeContext
            {
                Scene = scene,
                Position = spawn.Transform.WorldPosition,
                Rotation = spawn.Transform.WorldRotation
            },
            new Player.Prototype.Options
            {
                Input = _playerInput,
                Camera = camera,
                Physics = physics
            });

        scene.CreateObject("World UI example").AddComponent(new WorldUiDemo(ui,physics));

        scene.CreateObject("Rain weather").AddComponent(
            new RainWeather(assets, sceneInstantiator, audio));
    }

    public void OnUnload(SceneInstance scene)
    {
        _playerInput?.Dispose();
        _playerInput = null;
        _controlsAsset?.Dispose();
        _controlsAsset = null;
    }
}
