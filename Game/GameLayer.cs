using System.Numerics;
using Vecxy.Assets;
using Vecxy.Engine;
using Vecxy.Engine.Objects;
using Vecxy.Engine.Scenes;
using Vecxy.Rendering;
using Vecxy.UI;

namespace Game;

public sealed class GameLayer : AppLayer
{
    public SceneManager Scenes { get; set; } = null!;
    public IInput Input { get; set; } = null!;
    public AssetsManager Assets { get; set; } = null!;
    public UiSystem UI { get; set; } = null!;

    public override void OnInitialize()
    {
        var scene = Scenes.CreateScene("Main");

        var camera = scene.CreateObject("Main Camera");
        camera.Transform.Position = new Vector3(0f, 3f, 9f);
        camera.Transform.Rotation = Quaternion.CreateFromYawPitchRoll(0f, -0.2f, 0f);
        camera.AddScript(new CameraScript(Input));

        var modelAsset = Assets.Get<ModelAsset>("Test.glb")
            ?? throw new InvalidOperationException("Unable to load Test.glb");
        scene.Instantiate(modelAsset, "Test Model");
        Scenes.Load(scene);
        UI.Load(Assets.Get<TextAsset>("UI/Stats.uxml") ?? throw new InvalidOperationException("UI/Stats.uxml not found."),
            Assets.Get<TextAsset>("UI/Stats.css") ?? throw new InvalidOperationException("UI/Stats.css not found."));
    }
}
