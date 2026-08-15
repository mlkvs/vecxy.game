using System.Numerics;
using HardCore.Cultivation.Game.Infrastructure;
using JetBrains.Annotations;
using Vecxy.Assets;
using Vecxy.Kernel;
using Vecxy.Rendering;
using Vecxy.Scene;

namespace HardCore.Cultivation.Game;

public class Character(
    IAssetsManager assets,
    SpriteRenderer sprite,
    float referenceTextureHeight) : AComponent
{
    [UsedImplicitly]
    public class Prototype(IAssetsManager assets) : APrototype<Character, Prototype.Options>
    {
        public class Options : IPrototype.IOptions
        {
            public float Amplitude { get; init; } = 24.0f;
            public float PeriodSeconds { get; init; } = 3.6f;
        }
    
        public class Context : InstantiateContext
        {
            public string Name { get; init; } = "Character";
        }
        
        protected override Character Instantiate(InstantiateContext ctx)
        {
            if (ctx.Scene == null)
            {
                throw new NotImplementedException();
            }

            var ctxCast = (Context)ctx;
            
            // Scene Object
            var characterObject = ctx.Scene.CreateObject(ctxCast.Name);
            if (ctx.Parent != null)
            {
                characterObject.SetParent(ctx.Parent);
            }
            characterObject.Transform.Position = ctx.Position;
            characterObject.Transform.Rotation = ctx.Rotation;
            characterObject.Transform.Scale = ctx.Scale;
                
            // Sprite
            using var characterTexture = assets.Load<TextureAsset>("Textures/Character.png");
                
            var sprite = characterObject.AddComponent<SpriteRenderer>();
            sprite.SetTexture(characterTexture);
            sprite.PixelsPerUnit = 1.0f;
            sprite.Pivot = new Vector2(0.5f, 0.0f);
            sprite.SortingLayer = 1;

            // Character
            var character = characterObject.AddComponent(new Character(
                assets,
                sprite,
                characterTexture.Value.Height));
            character.PrewarmTextures();
                
            return character;

        }

        protected override void Configure(Character component, Options options)
        {
            component.Amplitude = options.Amplitude;
            component.PeriodSeconds = options.PeriodSeconds;
        }
    }
    
    private Vector3 _origin;
    private float _elapsed;
    private readonly Dictionary<string, AssetRef<TextureAsset>> _textureCache =
        new(StringComparer.Ordinal);
    private bool _gpuPrewarmed;

    public float Amplitude { get; set; } = 24.0f;
    public float PeriodSeconds { get; set; } = 3.6f;

    private bool _missionMode;

    private void PrewarmTextures()
    {
        _ = GetTexture("Textures/Character.png");
        _ = GetTexture("Textures/Character_Missions_Transparent.png");
    }

    public void PrewarmTextures(IRenderer renderer)
    {
        if (_gpuPrewarmed)
            return;
        PrewarmTextures();
        foreach (var texture in _textureCache.Values)
            renderer.PreloadTexture(texture);
        _gpuPrewarmed = true;
    }

    private AssetRef<TextureAsset> GetTexture(string path)
    {
        if (_textureCache.TryGetValue(path, out var texture))
            return texture;
        texture = assets.Load<TextureAsset>(path);
        _textureCache.Add(path, texture);
        return texture;
    }

    public void SetMissionMode(bool missionMode)
    {
        if (_missionMode == missionMode)
            return;

        _missionMode = missionMode;
        var texture = GetTexture(missionMode
            ? "Textures/Character_Missions_Transparent.png"
            : "Textures/Character.png");
        sprite.SetTexture(texture);
        sprite.PixelsPerUnit = texture.Value.Height / referenceTextureHeight;
    }

    public override void OnDestroy()
    {
        foreach (var texture in _textureCache.Values)
            texture.Dispose();
        _textureCache.Clear();
    }

    public override void Start()
    {
        _origin = Transform.Position;
    }

    public override void Update(float deltaTime)
    {
        _elapsed += deltaTime;
        var angularSpeed = MathF.Tau / Math.Max(0.1f, PeriodSeconds);
        Transform.Position = _origin + Vector3.UnitY * (MathF.Sin(_elapsed * angularSpeed) * Amplitude);
    }
}
