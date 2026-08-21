using System.Numerics;
using Vecxy.Assets;
using Vecxy.Input;
using Vecxy.Rendering;
using Vecxy.Scene;

namespace Sponza;

public sealed class MainScene(
    IInputManager input,
    IAssetsManager assets,
    ISceneInstantiator sceneInstantiator,
    IComponentInstantiator instantiator) : IScene
{
    private AssetRef<InputAsset>? _controlsAsset;
    private AssetRef<ModelAsset>? _sponzaAsset;
    private InputMap? _cameraInput;
    private Model? _sponza;

    public void OnLoad(SceneInstance scene)
    {
        _controlsAsset = assets.Load<InputAsset>("Controls.input");
        _cameraInput = input.Create(_controlsAsset, "Camera");
        _cameraInput.Enable();

        ConfigureLighting(scene);

        _sponzaAsset = assets.Load<ModelAsset>("Models/Sponza.glb");
        _sponza = new Model(_sponzaAsset);
        sceneInstantiator.InstantiateModel(scene, _sponza, "Sponza");

        var camera = instantiator.Instantiate<Camera>(
            new InstantiateContext { Scene = scene },
            new Camera.Prototype.Options
            {
                ClearColor = new Vector4(0.035f, 0.045f, 0.065f, 1.0f),
                NearPlane = 0.03f,
                FarPlane = 500.0f
            });
        camera.SceneObject!.Name = "Free Camera";
        camera.Transform.Position = new Vector3(8.0f, 2.2f, 0.0f);
        camera.Transform.Rotation = Quaternion.CreateFromYawPitchRoll(
            MathF.PI * 0.5f,
            0.0f,
            0.0f);

        instantiator.Instantiate<FreeFlyCamera>(
            new InstantiateContext { Parent = camera.SceneObject },
            new FreeFlyCamera.Prototype.Options
            {
                Input = _cameraInput,
                MoveSpeed = 5.0f,
                SprintMultiplier = 4.0f,
                LookSensitivity = 0.0025f
            });
    }

    public void OnUnload(SceneInstance scene)
    {
        _sponza?.Dispose();
        _sponza = null;
        _sponzaAsset?.Dispose();
        _sponzaAsset = null;
        _cameraInput?.Dispose();
        _cameraInput = null;
        _controlsAsset?.Dispose();
        _controlsAsset = null;
    }

    private static void ConfigureLighting(SceneInstance scene)
    {
        scene.Lighting.Skybox.Enabled = false;
        scene.Lighting.AmbientSkyColor = new Vector3(0.24f, 0.3f, 0.42f);
        scene.Lighting.AmbientGroundColor = new Vector3(0.045f, 0.038f, 0.032f);
        scene.Lighting.AmbientIntensity = 0.45f;
        scene.Lighting.DirectLightIntensityScale = 1.0f;
        scene.Lighting.SpecularStrength = 0.2f;
        scene.Lighting.Exposure = 0.85f;

        CreateDirectionalLight(
            scene,
            "Directional — Atrium Sun",
            new Vector3(0.3f, -0.9f, -0.25f),
            new Vector3(1.0f, 0.9f, 0.72f),
            1.4f);

        CreatePointLight(scene, "Point — West Gallery", new Vector3(-10.0f, 4.2f, 0.0f));
        CreatePointLight(scene, "Point — Center West", new Vector3(-3.5f, 4.2f, 0.0f));
        CreatePointLight(scene, "Point — Center East", new Vector3(3.5f, 4.2f, 0.0f));
        CreatePointLight(scene, "Point — East Gallery", new Vector3(10.0f, 4.2f, 0.0f));

        CreateSpotLight(
            scene,
            "Spot — West Floor",
            new Vector3(-8.0f, 9.0f, -2.5f),
            new Vector3(-8.0f, 0.0f, 0.0f));
        CreateSpotLight(
            scene,
            "Spot — Center Floor",
            new Vector3(0.0f, 9.0f, 2.5f),
            Vector3.Zero);
        CreateSpotLight(
            scene,
            "Spot — East Floor",
            new Vector3(8.0f, 9.0f, -2.5f),
            new Vector3(8.0f, 0.0f, 0.0f));
    }

    private static void CreateDirectionalLight(
        SceneInstance scene,
        string name,
        Vector3 direction,
        Vector3 color,
        float intensity)
    {
        var lightObject = scene.CreateObject(name);
        lightObject.Transform.Rotation = RotationForDirection(direction);
        var light = lightObject.AddComponent<DirectionalLight>();
        light.Color = color;
        light.Intensity = intensity;
    }

    private static void CreatePointLight(
        SceneInstance scene,
        string name,
        Vector3 position)
    {
        var lightObject = scene.CreateObject(name);
        lightObject.Transform.Position = position;
        var light = lightObject.AddComponent<PointLight>();
        light.Color = new Vector3(1.0f, 0.67f, 0.38f);
        light.Intensity = 32.0f;
        light.Range = 8.0f;
    }

    private static void CreateSpotLight(
        SceneInstance scene,
        string name,
        Vector3 position,
        Vector3 target)
    {
        var lightObject = scene.CreateObject(name);
        lightObject.Transform.Position = position;
        lightObject.Transform.Rotation = RotationForDirection(target - position);
        var light = lightObject.AddComponent<SpotLight>();
        light.Color = new Vector3(0.62f, 0.78f, 1.0f);
        light.Intensity = 90.0f;
        light.Range = 12.0f;
        light.OuterConeAngle = MathF.PI / 5.0f;
        light.InnerConeAngle = MathF.PI / 9.0f;
    }

    private static Quaternion RotationForDirection(Vector3 direction)
    {
        direction = Vector3.Normalize(direction);
        var yaw = MathF.Atan2(-direction.X, -direction.Z);
        var pitch = MathF.Asin(Math.Clamp(direction.Y, -1.0f, 1.0f));
        return Quaternion.CreateFromYawPitchRoll(yaw, pitch, 0.0f);
    }
}
