using System.Numerics;
using Vecxy.Assets;
using Vecxy.Input;
using Vecxy.Physics;
using Vecxy.Rendering;
using Vecxy.Scene;

namespace Horror;

public sealed class MainScene(
    IInputManager input,
    IAssetsManager assets,
    IConfigProvider configs,
    IRenderer renderer,
    ISceneInstantiator sceneInstantiator,
    IPhysicsSystem physics,
    IComponentInstantiator instantiator) : IScene
{
    private AssetRef<InputAsset>? _controlsAsset;
    private AssetRef<ModelAsset>? _roomAsset;
    private Model? _room;
    private InputMap? _playerInput;

    public void OnLoad(SceneInstance scene)
    {
        // --------------------------------
        // Input
        // --------------------------------

        _controlsAsset =
            assets.Load<InputAsset>("Controls.input");

        _playerInput =
            input.Create(
                _controlsAsset,
                "Player");

        _playerInput.Enable();

        // --------------------------------
        // Lighting
        // --------------------------------

        scene.Lighting.Skybox.Enabled = true;
        scene.Lighting.Exposure = 0.01f;
        

        _roomAsset = assets.Load<ModelAsset>("Models/room.glb");
        _room = new Model(_roomAsset);
        var roomRoot = sceneInstantiator.InstantiateModel(scene, _room, "Room");
        var playerSpawn = roomRoot.EnumerateHierarchy()
            .FirstOrDefault(sceneObject => string.Equals(
                sceneObject.Name,
                "SPAWN_PLAYER",
                StringComparison.OrdinalIgnoreCase));
        var playerPosition = playerSpawn?.Transform.WorldPosition
            ?? new Vector3(0.0f, 2.0f, 4.0f);

        // --------------------------------
        // Floor
        // --------------------------------

        using var floorMaterialAsset =
            assets.Load<MaterialAsset>("Materials/Default.material");
        var floorMaterial = new Material(floorMaterialAsset);
        floorMaterial.SetVector(
            "uTint",
            new Vector4(0.16f, 0.18f, 0.16f, 1.0f));

        instantiator.Instantiate<Floor>(
            new InstantiateContext { Scene = scene },
            new Floor.Prototype.Options
            {
                Position = new Vector3(
                    playerPosition.X,
                    playerPosition.Y - 3f,
                    playerPosition.Z),
                Size = new Vector2(20.0f, 20.0f),
                Thickness = 1.0f,
                Mesh = renderer.CreatePlane(),
                Material = floorMaterial
            });

        // --------------------------------
        // Player
        // --------------------------------

        var player =
            scene.CreateObject("Player");

        player.Transform.Position = playerPosition;

        var camera = instantiator.Instantiate<Camera>(
            new InstantiateContext { Scene = scene },
            new Camera.Prototype.Options
            {
                ClearColor = new Vector4(0.015f, 0.01f, 0.02f, 1.0f),
                NearPlane = 0.05f,
                FarPlane = 200.0f
            });
        camera.SceneObject!.Name = "Camera";
        camera.UsePostProcessing = true;

        var postProcessing = scene.CreateObject("Post Processing")
            .AddComponent(new PostProcessing(configs));

        instantiator.Instantiate<Player>(
            new InstantiateContext
            {
                Parent = player
            },
            new Player.Prototype.Options
            {
                Input = _playerInput,

                Camera = camera,

                Physics = physics,

                WalkSpeed = 3.5f,
                SprintMultiplier = 1.8f,
                GroundAcceleration = 24.0f,
                GroundDeceleration = 30.0f,
                AirAcceleration = 8.0f,
                JumpVelocity = 5.2f,
                LookSensitivity = 0.0025f,
                MaximumPitch = 1.45f,
                Height = 1.8f,
                Radius = 0.35f,
                EyeHeight = 1.62f,
                GroundProbeDistance = 0.15f
            });
    }

    public void OnUnload(SceneInstance scene)
    {
        _room?.Dispose();
        _room = null;
        _roomAsset?.Dispose();
        _roomAsset = null;

        _playerInput?.Dispose();
        _playerInput = null;

        _controlsAsset?.Dispose();
        _controlsAsset = null;
    }
}
