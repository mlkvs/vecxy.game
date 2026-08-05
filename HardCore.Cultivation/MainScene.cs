using System.Numerics;
using HardCore.Cultivation.Game;
using JetBrains.Annotations;
using Vecxy.Rendering;
using Vecxy.Scene;

namespace HardCore.Cultivation;

[UsedImplicitly]
public sealed class MainScene(IComponentInstantiator instantiator) : IScene
{
    public void OnLoad(SceneInstance scene)
    {
        scene.Lighting.Skybox.Enabled = true;
        
        var background = instantiator.Instantiate<Background>(scene);

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

        var camera = instantiator.Instantiate<Camera>
        (
            new InstantiateContext
            {
                Scene = scene,
                Position = new Vector3(0.0f, 0.0f, 10.0f),
            },
            new Camera.Prototype.Options
            {
                Projection = ECameraProjection.Orthographic,
                ClearColor = new Vector4(0.02f, 0.015f, 0.01f, 1.0f),
                OrthographicSize = background.SpriteRenderer.Texture.Height * 0.5f,
                NearPlane = 0.1f,
                FarPlane = 100.0f
            }
        );
    }
}