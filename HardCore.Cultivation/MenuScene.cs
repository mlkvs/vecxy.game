using System.Numerics;
using JetBrains.Annotations;
using Vecxy.Assets;
using Vecxy.Physics;
using Vecxy.Rendering;
using Vecxy.Scene;

namespace HardCore.Cultivation;

[UsedImplicitly]
public class MenuScene(
    IAssetsManager assets,
    CultivationInteraction cultivationInteraction) : IScene
{
    private const float BackgroundWidth = 941.0f;
    private const float BackgroundHeight = 1672.0f;

    public void OnLoad(SceneInstance scene)
    {
        var cameraObject = scene.CreateObject("Camera");
        cameraObject.Transform.Position = new Vector3(0.0f, 0.0f, 10.0f);
        var camera = cameraObject.AddComponent<Camera>();
        camera.Projection = ECameraProjection.Orthographic;
        camera.OrthographicSize = BackgroundHeight * 0.5f;
        camera.NearPlane = 0.1f;
        camera.FarPlane = 100.0f;
        camera.ClearColor = new Vector4(0.02f, 0.015f, 0.01f, 1.0f);

        var composition = scene.CreateObject("Cultivation composition");

        using var backgroundTexture =
            assets.Load<TextureAsset>("Textures/Background.png");
        var backgroundObject = composition.CreateChild("Forest background");
        var background = backgroundObject.AddComponent<SpriteRenderer>();
        background.SetTexture(backgroundTexture);
        background.PixelsPerUnit = 1.0f;
        background.SortingLayer = 0;

        using var characterTexture =
            assets.Load<TextureAsset>("Textures/Сharacter.png");
        var characterObject = composition.CreateChild("Cultivator");
        characterObject.Transform.Position = new Vector3(-27.5f, -450f, 0.0f);
        characterObject.Transform.Scale = new Vector3(0.675f, 0.675f, 1.0f);
        var character = characterObject.AddComponent<SpriteRenderer>();
        character.SetTexture(characterTexture);
        character.PixelsPerUnit = 1.0f;
        character.Pivot = new Vector2(0.5f, 0.0f);
        character.SortingLayer = 1;

        characterObject.AddComponent<BoxCollider2D>();
        characterObject.AddComponent(
            new CharacterPointerFeedback(
                character,
                cultivationInteraction));

        scene.Lighting.Skybox.Enabled = false;
    }
}
