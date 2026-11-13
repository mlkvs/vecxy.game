using Vecxy.Assets;
using Vecxy.Audio;
using Vecxy.Rendering;
using Vecxy.Scene;

namespace Horror;

/// <summary>World-space rain and ground impacts, independent of scene geometry.</summary>
public sealed class RainWeather(
    IAssetsManager assets,
    ISceneInstantiator sceneInstantiator,
    IAudioManager audio) : AComponent
{
    private readonly List<Material> _animatedMaterials = [];
    private AssetRef<ModelAsset>? _weatherAsset;
    private Model? _weather;
    private float _time;

    public override void Start()
    {
        using var rainAsset = assets.Load<MaterialAsset>("Materials/Rain.material");
        _weatherAsset = assets.Load<ModelAsset>("Models/Weather.glb");
        _weather = new Model(_weatherAsset);
        var weatherRoot = sceneInstantiator.InstantiateModel(SceneInstance, _weather, "Outside rain and splashes");
        foreach (var item in weatherRoot.EnumerateHierarchy())
        {
            if (item.GetComponent<MeshRenderer>() is not { } renderer)
                continue;

            var material = new Material(rainAsset);
            material.SetFloat("uSplash", item.Name.Contains("Splashes", StringComparison.Ordinal) ? 1.0f : 0.0f);
            renderer.SetMaterial(material);
            _animatedMaterials.Add(material);
        }

        audio.Play("Audio/Rain.wav", loop: true, volume: 0.22f);
    }

    public override void Update(float deltaTime)
    {
        _time += deltaTime;
        foreach (var material in _animatedMaterials)
            material.SetFloat("uTime", _time);
    }

    public override void OnDestroy()
    {
        audio.Stop("Audio/Rain.wav", loop: true);
        // MeshRenderer owns the material instances and releases them with the scene.
        _animatedMaterials.Clear();
        _weather?.Dispose();
        _weather = null;
        _weatherAsset?.Dispose();
        _weatherAsset = null;
    }
}
