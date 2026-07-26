using System.Numerics;
using ImGuiNET;
using JetBrains.Annotations;
using Vecxy.Assets;
using Vecxy.Editor;
using Vecxy.Engine;
using Vecxy.Input;
using Vecxy.Physics;
using Vecxy.Rendering;
using Vecxy.Scene;
using Vecxy.Diagnostics;

namespace Game;

[UsedImplicitly]
public sealed class GameLayer(
    IAssetsManager assets,
    IConfigProvider configs,
    IInputManager input,
    Vecxy.Kernel.IWindow window,
    IEditorGui editorGui,
    ISceneInstantiator sceneInstantiator,
    IPhysicsSystem physics,
    ISceneManager scenes
) : AAppLayer
{
    public sealed class Definition :
        ADefinition<GameLayer>;

    private AssetRef<Model>? _roomModel;
    private AssetRef<InputAsset>? _inputAsset;
    private AssetRef<Material>? _sceneMaterial;
    private ConfigRef<FogSettingsConfig>? _fogConfig;
    private ConfigRef<SkyboxSettingsConfig>? _skyboxConfig;
    private int _appliedFogConfigVersion = -1;
    private int _appliedSkyboxConfigVersion = -1;
    private PostProcessing? _postProcessing;
    private SceneObject? _sceneModelRoot;
    private Player? _player;
    private Scene? _scene;

    public override void OnInitialize()
    {
        configs.Register<FogSettingsConfig>();
        configs.Register<SkyboxSettingsConfig>();
        _inputAsset = assets.Load<InputAsset>("Controls.input");

        _roomModel = assets.Load<Model>("Models/Scene.glb");
        _sceneMaterial = assets.Load<Material>("Materials/Test.material");
        
        try
        {
            _scene = new Scene("Main");

            var roomObject = sceneInstantiator.InstantiateModel(
                _scene,
                _roomModel.Value,
                "Scene",
                _sceneMaterial.Value);
            _sceneModelRoot = roomObject;

            BuildScenePhysics(roomObject);
            _fogConfig = configs.LoadConfig<FogSettingsConfig>("Configs/Fog.yaml");
            _fogConfig.Changed += OnFogConfigChanged;
            _skyboxConfig = configs.LoadConfig<SkyboxSettingsConfig>("Configs/Skybox2.yaml");
            _skyboxConfig.Changed += OnSkyboxConfigChanged;
            if (_fogConfig.TryGetValue(out var fogConfig) && fogConfig is not null)
            {
                ApplyFogConfig(fogConfig);
            }
            else if (_fogConfig.LastError is not null)
            {
                Logger.Error(
                    _fogConfig.LastError,
                    $"Fog config is invalid: {_fogConfig.Path}");
            }

            if (_skyboxConfig.TryGetValue(out var skyboxConfig) && skyboxConfig is not null)
            {
                ApplySkyboxConfig(skyboxConfig);
            }
            else if (_skyboxConfig.LastError is not null)
            {
                Logger.Error(
                    _skyboxConfig.LastError,
                    $"Skybox config is invalid: {_skyboxConfig.Path}");
            }

            CreatePostProcessing();

            CreatePlayer(roomObject);
            
            scenes.SetActiveScene(_scene);
        }
        catch
        {
            DestroyScene();
            ReleaseAssets();
            throw;
        }
    }

    public override void OnUnload()
    {
        _sceneModelRoot = null;
        _player = null;
        _postProcessing = null;
        scenes.UnloadActiveScene();
        DestroyScene();
        ReleaseAssets();
    }

    public override void OnUpdate(float deltaTime)
    {
        RefreshFogConfig();
        RefreshSkyboxConfig();
        _player?.SyncView();
    }

    private void DestroyScene()
    {
        if (_scene is null)
            return;

        foreach (var root in
                 _scene.RootObjects.ToArray())
        {
            root.Destroy();
        }

        _scene = null;
    }

    private void ReleaseAssets()
    {
        if (_fogConfig is not null)
            _fogConfig.Changed -= OnFogConfigChanged;
        if (_skyboxConfig is not null)
            _skyboxConfig.Changed -= OnSkyboxConfigChanged;

        _roomModel?.Dispose();
        _inputAsset?.Dispose();
        _sceneMaterial?.Dispose();
        _fogConfig?.Dispose();
        _skyboxConfig?.Dispose();
        _roomModel = null;
        _inputAsset = null;
        _sceneMaterial = null;
        _fogConfig = null;
        _skyboxConfig = null;
    }

    private void CreatePlayer(SceneObject sceneRoot)
    {
        if (_scene is null || _inputAsset is null)
            return;

        var playerObject = _scene.CreateObject("Player");
        playerObject.Transform.Position =
            FindPlayerSpawn(sceneRoot);

        _player = playerObject.AddComponent(
            new Player(
                window,
                input,
                physics,
                _inputAsset));
        _player.WalkSpeed = 3.25f;
        _player.SprintMultiplier = 1.9f;
        _player.EyeHeight = 1.62f;
    }

    private void OnFogConfigChanged(FogSettingsConfig config)
    {
        ApplyFogConfig(config);
    }

    private void OnSkyboxConfigChanged(SkyboxSettingsConfig config)
    {
        ApplySkyboxConfig(config);
    }

    private void RefreshFogConfig()
    {
        if (_fogConfig is null)
            return;

        if (_fogConfig.Version == _appliedFogConfigVersion)
            return;

        if (_fogConfig.TryGetValue(out var config) && config is not null)
        {
            ApplyFogConfig(config);
        }
        else if (_fogConfig.LastError is not null)
        {
            Logger.Error(
                _fogConfig.LastError,
                $"Fog config reload rejected, keeping previous value: {_fogConfig.Path}");
            _appliedFogConfigVersion = _fogConfig.Version;
        }
    }

    private void ApplyFogConfig(FogSettingsConfig config)
    {
        if (_scene is null)
            return;

        var fog = _scene.Lighting.Fog;
        fog.Enabled = config.Enabled;
        fog.Mode = config.Mode;
        fog.Color = config.GetColor(_fogConfig?.Path ?? "Configs/Fog.yaml");
        fog.StartDistance = config.StartDistance;
        fog.EndDistance = config.EndDistance;
        fog.Density = config.Density;
        fog.HeightEnabled = config.HeightEnabled;
        fog.Height = config.Height;
        fog.HeightFalloff = config.HeightFalloff;
        fog.VolumetricStrength = config.VolumetricStrength;
        if (_fogConfig is not null)
            _appliedFogConfigVersion = _fogConfig.Version;
    }

    private void RefreshSkyboxConfig()
    {
        if (_skyboxConfig is null)
            return;

        if (_skyboxConfig.Version == _appliedSkyboxConfigVersion)
            return;

        if (_skyboxConfig.TryGetValue(out var config) && config is not null)
        {
            ApplySkyboxConfig(config);
        }
        else if (_skyboxConfig.LastError is not null)
        {
            Logger.Error(
                _skyboxConfig.LastError,
                $"Skybox config reload rejected, keeping previous value: {_skyboxConfig.Path}");
            _appliedSkyboxConfigVersion = _skyboxConfig.Version;
        }
    }

    private void ApplySkyboxConfig(SkyboxSettingsConfig config)
    {
        if (_scene is null)
            return;

        var skybox = _scene.Lighting.Skybox;
        skybox.Enabled = config.Enabled;
        skybox.PositiveX = config.PositiveX;
        skybox.NegativeX = config.NegativeX;
        skybox.PositiveY = config.PositiveY;
        skybox.NegativeY = config.NegativeY;
        skybox.PositiveZ = config.PositiveZ;
        skybox.NegativeZ = config.NegativeZ;
        skybox.Tint = config.GetTint(_skyboxConfig?.Path ?? "Configs/Skybox.yaml");
        skybox.Rotation = config.GetRotation(_skyboxConfig?.Path ?? "Configs/Skybox.yaml");
        skybox.Exposure = config.Exposure;

        if (_skyboxConfig is not null)
            _appliedSkyboxConfigVersion = _skyboxConfig.Version;
    }

    private void CreatePostProcessing()
    {
        if (_scene is null)
            return;

        var objectName = "Post Processing";
        var postObject = _scene.CreateObject(objectName);
        _postProcessing = postObject.AddComponent(
            new PostProcessing(configs));
    }

    private static Vector3 FindPlayerSpawn(SceneObject sceneRoot)
    {
        var floor =
            sceneRoot.FindChild("Floor")
            ?? sceneRoot.FindChild("Floor_01")
            ?? sceneRoot.FindChild("floor");

        if (floor?.GetComponentInChildren<MeshRenderer>() is not { } renderer)
            return new Vector3(0.0f, 0.05f, 3.0f);

        var localSpawn =
            renderer.LocalBoundsCenter +
            new Vector3(
                0.0f,
                0.05f,
                renderer.LocalBoundsSize.Z * 0.25f);

        return Vector3.Transform(localSpawn, floor.Transform.WorldMatrix);
    }

    private static void BuildScenePhysics(SceneObject root)
    {
        foreach (var sceneObject in root.EnumerateHierarchy())
        {
            if (sceneObject.GetComponent<MeshRenderer>() is not { } renderer)
                continue;

            var collider =
                sceneObject.GetComponent<BoxCollider>()
                ?? sceneObject.AddComponent<BoxCollider>();

            FitColliderToRenderer(collider, renderer);

            var body =
                sceneObject.GetComponent<RigidBody>()
                ?? sceneObject.AddComponent<RigidBody>();

            if (IsDynamicPhysicsObject(sceneObject.Name))
            {
                body.MotionType = EPhysicsMotionType.Dynamic;
                body.AffectedByGravity = true;
                body.Mass = 25.0f;
                body.Friction = 0.8f;
                body.Restitution = 0.0f;
                body.EnableSpeculativeContacts = true;
            }
            else
            {
                body.MotionType = EPhysicsMotionType.Static;
                body.AffectedByGravity = false;
                body.Friction = 0.8f;
                body.Restitution = 0.0f;
            }
        }
    }

    private static void FitColliderToRenderer(
        BoxCollider collider,
        MeshRenderer renderer)
    {
        collider.Center = renderer.LocalBoundsCenter;

        var size = renderer.LocalBoundsSize;
        collider.Size = new Vector3(
            Math.Max(size.X, 0.05f),
            Math.Max(size.Y, 0.05f),
            Math.Max(size.Z, 0.05f));
    }

    private static bool IsDynamicPhysicsObject(string name)
    {
        return name.Contains("Cube", StringComparison.OrdinalIgnoreCase) ||
               name.Contains("Dynamic", StringComparison.OrdinalIgnoreCase) ||
               name.Contains("Moveable", StringComparison.OrdinalIgnoreCase);
    }
}
