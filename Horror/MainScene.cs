using System.Numerics;
using Vecxy.Assets;
using Vecxy.Input;
using Vecxy.Physics;
using Vecxy.Prototypes;
using Vecxy.Rendering;
using Vecxy.Scene;

namespace Horror;

public sealed class MainScene(
    IInputManager input,
    IAssetsManager assets,
    IConfigProvider configs,
    ISceneInstantiator sceneInstantiator,
    IPhysicsSystem physics,
    IPrototypes prototypes) : IScene
{
    private AssetRef<InputAsset>? _controlsAsset;
    private InputMap? _playerInput;
    private AssetRef<ModelAsset>? _locationAsset;
    private Model? _location;
    private ConfigRef<LaundromatConfig>? _layout;

    public void OnLoad(SceneInstance scene)
    {
        _controlsAsset = assets.Load<InputAsset>("Controls.input");
        _playerInput = input.Create(_controlsAsset, "Player");
        _playerInput.Enable();

        scene.Lighting.Skybox.Enabled = false;
        scene.Lighting.Exposure = 1.0f;
        scene.Lighting.AmbientSkyColor = new Vector3(0.12f, 0.15f, 0.2f);
        scene.Lighting.AmbientGroundColor = new Vector3(0.035f, 0.04f, 0.045f);
        scene.Lighting.AmbientIntensity = 0.7f;
        scene.Lighting.SpecularStrength = 0.16f;
        scene.Lighting.Fog.Color = new Vector3(0.015f, 0.02f, 0.03f);
        scene.Lighting.Fog.StartDistance = 10.0f;
        scene.Lighting.Fog.EndDistance = 28.0f;
        scene.Lighting.Fog.HeightEnabled = false;
        scene.Lighting.Fog.VolumetricStrength = 0.08f;

        _locationAsset = assets.Load<ModelAsset>("Models/Laundromat.glb");
        _location = new Model(_locationAsset);
        sceneInstantiator.InstantiateModel(scene, _location, "Delicate Wash — Laundromat");

        // The generator exports matching collision bounds with the model, in metres.
        _layout = configs.LoadConfig<LaundromatConfig>("Configs/Laundromat.yaml");
        var layout = _layout.Value;
        foreach (var obstacle in layout.Colliders)
        {
            var body = scene.CreateObject(obstacle.Name, isStatic: true);
            body.Transform.Position = new Vector3(obstacle.Position[0], obstacle.Position[1], obstacle.Position[2]);
            body.Transform.Rotation = Quaternion.CreateFromAxisAngle(Vector3.UnitY, obstacle.Yaw);
            var collider = body.AddComponent<BoxCollider>();
            collider.Size = new Vector3(obstacle.Size[0], obstacle.Size[1], obstacle.Size[2]);
        }

        AddLight(scene, "Front fluorescent", new Vector3(2.0f, 2.72f, 2.2f),
            new Vector3(0.85f, 0.9f, 0.72f), 20.0f, 8.0f);
        AddLight(scene, "Back fluorescent", new Vector3(2.0f, 2.72f, -2.4f),
            new Vector3(0.85f, 0.9f, 0.72f), 26.0f, 8.0f);
        AddLight(scene, "Window fluorescent", new Vector3(-2.0f, 2.72f, -1.2f),
            new Vector3(0.78f, 0.85f, 0.7f), 18.0f, 7.0f);
        AddLight(scene, "Red 24-7 neon spill", new Vector3(-5.35f, 1.95f, 0.0f),
            new Vector3(1.0f, 0.035f, 0.018f), 9.0f, 5.0f);
        AddLight(scene, "Rainy night outside", new Vector3(-5.3f, 2.2f, -2.8f),
            new Vector3(0.23f, 0.38f, 0.75f), 5.0f, 7.0f);
        AddLight(scene, "Telephone indicator spill", new Vector3(-3.4f, 1.32f, 2.85f),
            new Vector3(1.0f, 0.08f, 0.025f), 0.7f, 1.5f);

        var cameraObject = scene.CreateObject("First Person Camera");
        var camera = cameraObject.AddComponent<Camera>();
        camera.ClearColor = new Vector4(0.015f, 0.02f, 0.03f, 1.0f);
        camera.FieldOfView = 75.0f;
        camera.NearPlane = 0.05f;
        camera.FarPlane = 60.0f;
        camera.UsePostProcessing = true;
        scene.CreateObject("Post Processing").AddComponent(new PostProcessing(configs));

        prototypes.Instantiate<Player>(
            new ScenePrototypeContext
            {
                Scene = scene,
                Position = new Vector3(layout.SpawnPosition[0], layout.SpawnPosition[1], layout.SpawnPosition[2]),
                Rotation = Quaternion.CreateFromAxisAngle(Vector3.UnitY, layout.SpawnYaw)
            },
            new Player.Prototype.Options
            {
                Input = _playerInput,
                Camera = camera,
                Physics = physics
            });
    }

    private static void AddLight(SceneInstance scene, string name, Vector3 position, Vector3 color, float intensity, float range)
    {
        var lightObject = scene.CreateObject(name);
        lightObject.Transform.Position = position;
        var light = lightObject.AddComponent<PointLight>();
        light.Color = color;
        light.Intensity = intensity;
        light.Range = range;
    }

    public void OnUnload(SceneInstance scene)
    {
        _location?.Dispose();
        _location = null;
        _locationAsset?.Dispose();
        _locationAsset = null;
        _layout?.Dispose();
        _layout = null;
        _playerInput?.Dispose();
        _playerInput = null;
        _controlsAsset?.Dispose();
        _controlsAsset = null;
    }
}
