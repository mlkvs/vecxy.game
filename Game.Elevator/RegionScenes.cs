using System.Numerics;
using Vecxy.Assets;
using Vecxy.Diagnostics;
using Vecxy.Rendering;
using Vecxy.Scene;

namespace Game.Elevator;

public readonly record struct RegionSceneStyle(
    int Id,
    string Name,
    Vector4 Tint,
    Vector4 ClearColor,
    Vector3 Scale,
    float InitialYaw,
    Vector3 RotationAxis,
    float RotationSpeed);

public abstract class RegionSceneBase(
    IAssetsManager assets,
    ISceneInstantiator sceneInstantiator,
    RegionSceneStyle style) : IScene
{
    private AssetRef<ModelAsset>? _modelAsset;
    private AssetRef<MaterialAsset>? _baseMaterialAsset;
    private Model? _model;
    private Material? _baseMaterial;

    public void OnLoad(SceneInstance scene)
    {
        _modelAsset = assets.Load<ModelAsset>("Models/Scene.glb");
        _baseMaterialAsset =
            assets.Load<MaterialAsset>("Materials/Default.material");
        _model = new Model(_modelAsset);
        _baseMaterial = new Material(_baseMaterialAsset);

        var fallbackMaterial = _baseMaterial.Clone();
        fallbackMaterial.SetVector("uTint", style.Tint);

        SceneObject displayObject;
        try
        {
            displayObject = sceneInstantiator.InstantiateModel(
                scene,
                _model,
                $"Region {style.Id}: {style.Name}",
                fallbackMaterial);
        }
        finally
        {
            fallbackMaterial.Dispose();
        }

        // Scene.glb contains a baked directional light. It must not inherit the
        // non-uniform display scale: every region has its own root key light.
        foreach (var importedLight in
                 displayObject.GetComponentsInChildren<ALight>())
        {
            importedLight.Enabled = false;
        }

        displayObject.Transform.Scale = style.Scale;
        displayObject.Transform.Rotation =
            Quaternion.CreateFromAxisAngle(Vector3.UnitY, style.InitialYaw);
        displayObject.AddComponent(
            new RotatingDisplay(style.RotationAxis, style.RotationSpeed));

        scene.Lighting.Skybox.Enabled = false;
        scene.Lighting.AmbientSkyColor = new Vector3(0.32f, 0.35f, 0.42f);
        scene.Lighting.AmbientGroundColor = new Vector3(0.08f, 0.07f, 0.06f);
        scene.Lighting.Exposure = 0.004f;

        var cameraObject = scene.CreateObject($"{style.Name} Camera");
        cameraObject.Transform.Position = new Vector3(6.2f, 4.6f, 7.4f);
        cameraObject.Transform.LookAt(new Vector3(0.0f, 0.45f, 0.0f));
        var camera = cameraObject.AddComponent<Camera>();
        camera.ClearColor = style.ClearColor;
        camera.FieldOfView = 52.0f;

        var lightObject = scene.CreateObject($"{style.Name} Key Light");
        lightObject.Transform.Position = new Vector3(4.0f, 7.0f, 5.0f);
        lightObject.Transform.LookAt(Vector3.Zero);
        var light = lightObject.AddComponent<DirectionalLight>();
        light.Color = new Vector3(style.Tint.X, style.Tint.Y, style.Tint.Z);
        light.Intensity = 520.0f;

        Logger.Info($"Loaded region scene {style.Id} / {style.Name}.");
    }

    public void OnUnload(SceneInstance scene)
    {
        _model?.Dispose();
        _model = null;
        _modelAsset?.Dispose();
        _modelAsset = null;
        _baseMaterial?.Dispose();
        _baseMaterial = null;
        _baseMaterialAsset?.Dispose();
        _baseMaterialAsset = null;

        Logger.Info($"Unloaded region scene {style.Id} / {style.Name}.");
    }
}

public sealed class RotatingDisplay(
    Vector3 axis,
    float radiansPerSecond) : AComponent
{
    private readonly Vector3 _axis = axis.LengthSquared() > float.Epsilon
        ? Vector3.Normalize(axis)
        : Vector3.UnitY;

    public override void Update(float deltaTime)
    {
        Transform.Rotate(
            Quaternion.CreateFromAxisAngle(
                _axis,
                radiansPerSecond * deltaTime));
    }
}

public sealed class ApartmentScene(
    IAssetsManager assets,
    ISceneInstantiator sceneInstantiator) : RegionSceneBase(
        assets,
        sceneInstantiator,
        new RegionSceneStyle(
            1, "Apartment",
            new Vector4(1.0f, 0.24f, 0.22f, 1.0f),
            new Vector4(0.10f, 0.025f, 0.025f, 1.0f),
            new Vector3(0.92f, 1.10f, 0.92f),
            0.10f, Vector3.UnitY, 0.18f));

public sealed class BusStationScene(
    IAssetsManager assets,
    ISceneInstantiator sceneInstantiator) : RegionSceneBase(
        assets,
        sceneInstantiator,
        new RegionSceneStyle(
            2, "Bus Station",
            new Vector4(0.25f, 1.0f, 0.35f, 1.0f),
            new Vector4(0.02f, 0.09f, 0.035f, 1.0f),
            new Vector3(1.18f, 0.72f, 0.82f),
            0.35f, Vector3.UnitY, -0.22f));

public sealed class FactoryScene(
    IAssetsManager assets,
    ISceneInstantiator sceneInstantiator) : RegionSceneBase(
        assets,
        sceneInstantiator,
        new RegionSceneStyle(
            3, "Factory",
            new Vector4(0.25f, 0.42f, 1.0f, 1.0f),
            new Vector4(0.018f, 0.03f, 0.11f, 1.0f),
            new Vector3(1.25f, 1.25f, 1.25f),
            0.60f, new Vector3(0.0f, 1.0f, 0.15f), 0.28f));

public sealed class HouseScene(
    IAssetsManager assets,
    ISceneInstantiator sceneInstantiator) : RegionSceneBase(
        assets,
        sceneInstantiator,
        new RegionSceneStyle(
            4, "House",
            new Vector4(1.0f, 0.26f, 0.92f, 1.0f),
            new Vector4(0.10f, 0.02f, 0.09f, 1.0f),
            new Vector3(0.82f, 1.32f, 0.82f),
            0.85f, Vector3.UnitY, -0.16f));

public sealed class LakeScene(
    IAssetsManager assets,
    ISceneInstantiator sceneInstantiator) : RegionSceneBase(
        assets,
        sceneInstantiator,
        new RegionSceneStyle(
            5, "Lake",
            new Vector4(0.20f, 0.92f, 1.0f, 1.0f),
            new Vector4(0.015f, 0.075f, 0.10f, 1.0f),
            new Vector3(1.20f, 0.50f, 1.20f),
            1.10f, new Vector3(0.08f, 1.0f, 0.0f), 0.12f));

public sealed class BridgeScene(
    IAssetsManager assets,
    ISceneInstantiator sceneInstantiator) : RegionSceneBase(
        assets,
        sceneInstantiator,
        new RegionSceneStyle(
            6, "Bridge",
            new Vector4(1.0f, 0.92f, 0.18f, 1.0f),
            new Vector4(0.11f, 0.085f, 0.01f, 1.0f),
            new Vector3(1.42f, 0.68f, 0.72f),
            1.35f, Vector3.UnitY, 0.20f));

public sealed class ParkScene(
    IAssetsManager assets,
    ISceneInstantiator sceneInstantiator) : RegionSceneBase(
        assets,
        sceneInstantiator,
        new RegionSceneStyle(
            7, "Park",
            new Vector4(0.78f, 0.20f, 0.16f, 1.0f),
            new Vector4(0.065f, 0.018f, 0.015f, 1.0f),
            new Vector3(0.76f, 0.92f, 1.35f),
            1.60f, new Vector3(0.12f, 1.0f, 0.10f), -0.26f));

public sealed class WarehouseScene(
    IAssetsManager assets,
    ISceneInstantiator sceneInstantiator) : RegionSceneBase(
        assets,
        sceneInstantiator,
        new RegionSceneStyle(
            8, "Warehouse",
            new Vector4(0.72f, 0.30f, 0.78f, 1.0f),
            new Vector4(0.065f, 0.02f, 0.075f, 1.0f),
            new Vector3(1.30f, 0.88f, 1.08f),
            1.85f, Vector3.UnitY, 0.30f));

public sealed class TowerScene(
    IAssetsManager assets,
    ISceneInstantiator sceneInstantiator) : RegionSceneBase(
        assets,
        sceneInstantiator,
        new RegionSceneStyle(
            9, "Tower",
            new Vector4(1.0f, 0.46f, 0.16f, 1.0f),
            new Vector4(0.10f, 0.035f, 0.012f, 1.0f),
            new Vector3(0.68f, 1.55f, 0.68f),
            2.10f, new Vector3(0.06f, 1.0f, 0.0f), -0.18f));
