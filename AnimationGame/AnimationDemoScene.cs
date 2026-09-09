using System.Numerics;
using Vecxy.Animations;
using Vecxy.Assets;
using Vecxy.Rendering;
using Vecxy.Scene;

namespace AnimationGame;

public sealed class AnimationDemoScene(
    IAssetsManager assets,
    IAnimationSystem animations) : IScene
{
    private AssetRef<ModelAsset>? _foxAsset;
    private Model? _foxModel;

    public void OnLoad(SceneInstance scene)
    {
        scene.Lighting.Skybox.Enabled = false;
        scene.Lighting.AmbientSkyColor = new Vector3(0.35f, 0.42f, 0.55f);
        scene.Lighting.AmbientGroundColor = new Vector3(0.08f, 0.06f, 0.05f);
        scene.Lighting.AmbientIntensity = 0.8f;
        scene.Lighting.Exposure = 1.0f;

        _foxAsset = assets.Load<ModelAsset>("Models/Fox.glb");
        _foxModel = new Model(_foxAsset);
        var actor = animations.Instantiate(
            scene,
            _foxModel,
            CharacterAnimation.Create(_foxModel),
            "Animated Fox");
        actor.Root.Transform.Position = new Vector3(0.0f, 0.0f, 0.0f);
        actor.Root.Transform.Scale = new Vector3(0.015f);
        actor.Root.AddComponent(new AnimationDemoDriver(actor.Animator));

        actor.Animator.EventFired += animationEvent =>
            Console.WriteLine($"Animation event: {animationEvent.Name} ({animationEvent.State})");

        var cameraObject = scene.CreateObject("Camera");
        cameraObject.Transform.Position = new Vector3(0.0f, 1.0f, 4.0f);
        cameraObject.Transform.LookAt(new Vector3(0.0f, 0.75f, 0.0f));
        var camera = cameraObject.AddComponent<Camera>();
        camera.ClearColor = new Vector4(0.025f, 0.035f, 0.055f, 1.0f);
        camera.NearPlane = 0.03f;
        camera.FarPlane = 100.0f;

        var lightObject = scene.CreateObject("Key Light");
        lightObject.Transform.Rotation = Quaternion.CreateFromYawPitchRoll(-0.7f, -0.8f, 0.0f);
        var light = lightObject.AddComponent<DirectionalLight>();
        light.Color = new Vector3(1.0f, 0.9f, 0.75f);
        light.Intensity = 2.0f;
    }

    public void OnUnload(SceneInstance scene)
    {
        _foxModel?.Dispose();
        _foxModel = null;
        _foxAsset?.Dispose();
        _foxAsset = null;
    }
}
