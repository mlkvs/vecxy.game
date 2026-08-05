using System.Numerics;
using HardCore.Cultivation.Game;
using JetBrains.Annotations;
using Vecxy.Assets;
using Vecxy.Rendering;
using Vecxy.Scene;

namespace HardCore.Cultivation;

[UsedImplicitly]
public sealed class MainScene
(
    IAssetsManager assets,
    IComponentInstantiator instantiator
) : IScene
{
    public void OnLoad(SceneInstance scene)
    {
        var background = CreateBackground(scene);

        var character = instantiator.Instantiate<Character>
        (
            new Character.Prototype.Context
            {
                Name = "Cultivator",
                Scene = scene, 
                Position = new Vector3(0.0f, -450.0f, 0.0f), 
                Scale = new Vector3(0.70f, 0.70f, 1.0f),
            }
        );
        
        CreateCamera(scene, background);
    }

    public void OnUnload(SceneInstance scene)
    {
    }

    private void CreateCamera(SceneInstance scene, SpriteRenderer background)
    {
        var cameraObject = scene.CreateObject("Camera");
        cameraObject.Transform.Position = new Vector3(0.0f, 0.0f, 10.0f);

        var camera = cameraObject.AddComponent<Camera>();
        camera.Projection = ECameraProjection.Orthographic;
        camera.OrthographicSize = background.Texture.Height * 0.5f;
        camera.NearPlane = 0.1f;
        camera.FarPlane = 100.0f;
        camera.ClearColor = new Vector4(0.02f, 0.015f, 0.01f, 1.0f);
    }

    private SpriteRenderer CreateBackground(SceneInstance scene)
    {
        using var backgroundTexture =
            assets.Load<TextureAsset>("Textures/Background.png");

        var backgroundObject = scene.CreateObject("Background");

        var background = backgroundObject.AddComponent<SpriteRenderer>();
        background.SetTexture(backgroundTexture);
        background.PixelsPerUnit = 1.0f;
        background.SortingLayer = 0;

        return background;
    }
}
