using System.Numerics;
using JetBrains.Annotations;
using Vecxy.Assets;
//using Vecxy.Audio;
using Vecxy.Engine;
using Vecxy.Input;
using Vecxy.Kernel;
using Vecxy.Physics;
using Vecxy.Rendering;
using Vecxy.Scene;
using Vecxy.Diagnostics;
using Vecxy.Diagnostics.Console;
using Vecxy.UI;

namespace Game;

[UsedImplicitly]
public sealed class GameLayer(
    IAssetsManager assets,
    IConfigProvider configs,
    IInputManager input,
    Vecxy.Kernel.IWindow window,
    ISceneInstantiator sceneInstantiator,
    IPhysicsSystem physics,
    ISceneManager scenes,
    IConsoleRegistry consoleRegistry,
   // IAudioManager audioManager,
    ISceneFactory sceneFactory,
    IUiManager ui
) : AAppLayer
{
    public sealed class Definition : ADefinition<GameLayer>
    {
        public override IReadOnlyList<Vecxy.Kernel.IDefinition> Children =>
            [new UiModule.Definition()];
    }

    private AssetRef<Model>? _roomModel;
    private AssetRef<InputAsset>? _inputAsset;
    private AssetRef<Material>? _sceneMaterial;
    private ConfigRef<FogSettingsConfig>? _fogConfig;
    private ConfigRef<SkyboxSettingsConfig>? _skyboxConfig;
    private int _appliedFogConfigVersion = -1;
    private int _appliedSkyboxConfigVersion = -1;
    private PostProcessing? _postProcessing;
    private SceneObject? _sceneModelRoot;
    private SceneObject? _sunLightObject;
    private Player? _player;
    private PlayerDebugTarget? _playerDebugTarget;
    private Scene? _scene;
    private UiDocumentHandle? _hudDocument;
    private readonly IModule? _uiModule = ui as IModule;

    public override void OnInitialize()
    {
        _uiModule?.OnInitialize();
        configs.Register<FogSettingsConfig>();
        configs.Register<SkyboxSettingsConfig>();
        _inputAsset = assets.Load<InputAsset>("Controls.input");

        _roomModel = assets.Load<Model>("Models/Trees.glb");
        _sceneMaterial = assets.Load<Material>("Materials/Test.material");
        
        var avocado = assets.Load<Model>("Models/Duck.glb");
        
        try
        {
            _scene = sceneFactory.Create();

            var roomObject = sceneInstantiator.InstantiateModel(
                _scene,
                _roomModel.Value,
                "Trees",
                _sceneMaterial.Value);
            _sceneModelRoot = roomObject;
            _sunLightObject = CreateSunLight(roomObject);

            var avocadoModel = sceneInstantiator.InstantiateModel(_scene, avocado.Value, "Avocado");
            
            avocadoModel.SetParent(roomObject);
            
            avocadoModel.Transform.Position = FindPlayerSpawn(roomObject);
            avocadoModel.Transform.Scale = new Vector3(5, 5, 5);
            

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
            RegisterConsoleTargets();
            _hudDocument = ui.ShowDocument(
                "UI/MinimalHud.rml",
                "UI/MinimalHud.rcss",
                "Minimal HUD");
            SyncHud();
            
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
        _sunLightObject = null;
        _player = null;
        if (_playerDebugTarget is not null)
            consoleRegistry.Unregister(_playerDebugTarget);
        _playerDebugTarget = null;
        _postProcessing = null;
        _hudDocument?.Dispose();
        _hudDocument = null;
        scenes.UnloadActiveScene();
        DestroyScene();
        ReleaseAssets();
        _uiModule?.OnShutdown();
    }

    public override void OnUpdate(float deltaTime)
    {
        RefreshFogConfig();
        RefreshSkyboxConfig();
        _player?.SyncView();
        SyncHud();
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

    private void SyncHud()
    {
        if (_hudDocument is null || _player is null)
            return;

        _hudDocument.SetNumber("player-health:value", _player.Health);
        _hudDocument.SetNumber("player-health:max", _player.MaxHealth);
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

    private void RegisterConsoleTargets()
    {
        if (_player is null)
            return;

        _playerDebugTarget = new PlayerDebugTarget(_player);
        consoleRegistry.Register(_playerDebugTarget);
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
            ?? sceneRoot.FindChild("floor")
            ?? sceneRoot.FindChild("Plane");

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

    private SceneObject? CreateSunLight(SceneObject sceneRoot)
    {
        if (_scene is null)
            return null;

        var sunObject =
            sceneRoot.FindChild("Sun")
            ?? _scene.CreateObject("Sun");

        if (sunObject.Parent is null &&
            !ReferenceEquals(sunObject, sceneRoot))
        {
            sunObject.SetParent(sceneRoot, worldPositionStays: false);
        }

        if (sunObject.GetComponent<DirectionalLight>() is not { } sunLight)
        {
            sunLight = sunObject.AddComponent<DirectionalLight>();
            sunLight.Color = Vector3.One;
            sunLight.Intensity = 683.0f;
        }

        if (sunObject.Name == "Sun" &&
            sunObject.Transform.LocalRotation == Quaternion.Identity)
        {
            sunObject.Transform.LocalRotation = new Quaternion(
                -0.8189004f,
                -0.28070855f,
                0.42471026f,
                0.26500204f);
        }

        return sunObject;
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
                sceneObject.GetComponent<RigidBody>();

            if (IsDynamicPhysicsObject(sceneObject.Name))
            {
                sceneObject.IsStatic = false;
                body ??= sceneObject.AddComponent<RigidBody>();
                body.MotionType = EPhysicsMotionType.Dynamic;
                body.AffectedByGravity = true;
                body.Mass = 25.0f;
                body.EnableSpeculativeContacts = true;
            }
            else
            {
                sceneObject.IsStatic = true;

                if (body is not null)
                    sceneObject.RemoveComponent(body);
            }

            collider.CollisionLayer =
                sceneObject.IsStatic ? "world" : "default";
            collider.Material.Friction = 0.8f;
            collider.Material.Restitution = 0.0f;

            if (collider.SceneObject.Name == "Cube")
            {
                collider.CollisionLayer = "box";
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
