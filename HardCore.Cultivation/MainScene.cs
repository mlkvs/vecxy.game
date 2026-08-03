using System.Numerics;
using JetBrains.Annotations;
using Vecxy.Assets;
using Vecxy.Rendering;
using Vecxy.Scene;

namespace HardCore.Cultivation;

[UsedImplicitly]
public sealed class MainScene
(
    IAssetsManager assets
) : IScene
{
    public void OnLoad(SceneInstance scene)
    {
        var background = CreateBackground(scene);
        CreateCharacter(scene);
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

    private SpriteRenderer CreateCharacter(SceneInstance scene)
    {
        using var characterTexture =
            assets.Load<TextureAsset>("Textures/Character.png");

        var characterObject = scene.CreateObject("Character");
        characterObject.Transform.Position = new Vector3(-27.5f, -450.0f, 0.0f);
        characterObject.Transform.Scale = new Vector3(0.675f, 0.675f, 1.0f);

        var character = characterObject.AddComponent<SpriteRenderer>();
        character.SetTexture(characterTexture);
        character.PixelsPerUnit = 1.0f;
        character.Pivot = new Vector2(0.5f, 0.0f);
        character.SortingLayer = 1;

        return character;
    }
}
