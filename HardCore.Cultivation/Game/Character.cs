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

            var dogConfig = new DogConfig();
            var dogObject = characterObject.CreateChild("Dog companion");
            dogObject.Transform.Position = new Vector3(dogConfig.LocalPositionX, dogConfig.LocalPositionY, 0.0f);
            dogObject.Transform.Scale = new Vector3(dogConfig.BaseScale, dogConfig.BaseScale, 1.0f);
            using var dogTexture = assets.Load<TextureAsset>(dogConfig.MeditatingTexture);
            var dogSprite = dogObject.AddComponent<SpriteRenderer>();
            dogSprite.SetTexture(dogTexture);
            dogSprite.PixelsPerUnit = 1.0f;
            dogSprite.Pivot = new Vector2(0.5f, 0.0f);
            dogSprite.SortingLayer = 2;
            using var chargedTexture = assets.Load<TextureAsset>(dogConfig.ChargedTexture);
            var chargedObject = dogObject.CreateChild("Dog meditation fill");
            var chargedSprite = chargedObject.AddComponent<SpriteRenderer>();
            chargedSprite.SetTexture(chargedTexture);
            chargedSprite.PixelsPerUnit = 1.0f;
            chargedSprite.Pivot = new Vector2(0.5f, 0.0f);
            chargedSprite.SortingLayer = 2;
            chargedSprite.OrderInLayer = 1;

            var glowSprites = new List<SpriteRenderer>(2);
            for (var index = 0; index < 2; index++)
            {
                var glowObject = dogObject.CreateChild($"Dog ready glow {index + 1}");
                var glowSprite = glowObject.AddComponent<SpriteRenderer>();
                glowSprite.SetTexture(chargedTexture);
                glowSprite.PixelsPerUnit = 1.0f;
                glowSprite.Pivot = new Vector2(0.5f, 0.0f);
                glowSprite.SortingLayer = 2;
                glowSprite.OrderInLayer = -2 + index;
                glowSprites.Add(glowSprite);
            }

            dogObject.AddComponent(new DogCompanion(assets, dogSprite, chargedSprite, glowSprites));
                
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

public sealed class DogCompanion(
    IAssetsManager assets,
    SpriteRenderer baseSprite,
    SpriteRenderer chargedSprite,
    IReadOnlyList<SpriteRenderer> glowSprites) : AComponent
{
    private DogConfig _config = new();
    private float _chargeProgress;
    private float _elapsed;
    private Vector3 _origin;
    private Vector3 _baseScale;
    private bool _missionMode;
    private readonly Dictionary<string, AssetRef<TextureAsset>> _textureCache =
        new(StringComparer.Ordinal);
    private bool _gpuPrewarmed;

    public override void Start()
    {
        _origin = Transform.Position;
        _baseScale = Transform.Scale;
        SetChargeProgress(0f);
    }

    public void Configure(DogConfig config)
    {
        _config = config;
        Transform.Position = new Vector3(config.LocalPositionX, config.LocalPositionY, 0f);
        Transform.Scale = new Vector3(config.BaseScale, config.BaseScale, 1f);
        _origin = Transform.Position;
        _baseScale = Transform.Scale;

        _ = GetTexture(config.MeditatingTexture);
        _ = GetTexture(config.ChargedTexture);
        _ = GetTexture(config.MissionMeditatingTexture);
        _ = GetTexture(config.MissionChargedTexture);

        ApplyTextures(
            _missionMode ? config.MissionMeditatingTexture : config.MeditatingTexture,
            _missionMode ? config.MissionChargedTexture : config.ChargedTexture);
        SetChargeProgress(_chargeProgress);
    }

    public void PrewarmTextures(IRenderer renderer)
    {
        if (_gpuPrewarmed)
            return;
        foreach (var texture in _textureCache.Values)
            renderer.PreloadTexture(texture);
        _gpuPrewarmed = true;
    }

    public void SetMissionMode(bool missionMode)
    {
        if (_missionMode == missionMode)
            return;

        _missionMode = missionMode;
        ApplyTextures(
            missionMode ? _config.MissionMeditatingTexture : _config.MeditatingTexture,
            missionMode ? _config.MissionChargedTexture : _config.ChargedTexture);
        SetChargeProgress(_chargeProgress);
    }

    private void ApplyTextures(string meditatingPath, string chargedPath)
    {
        var meditatingTexture = GetTexture(meditatingPath);
        baseSprite.SetTexture(meditatingTexture);
        var chargedTexture = GetTexture(chargedPath);
        chargedSprite.SetTexture(chargedTexture);
        foreach (var glow in glowSprites)
            glow.SetTexture(chargedTexture);
    }

    private AssetRef<TextureAsset> GetTexture(string path)
    {
        if (_textureCache.TryGetValue(path, out var texture))
            return texture;
        texture = assets.Load<TextureAsset>(path);
        _textureCache.Add(path, texture);
        return texture;
    }

    public override void OnDestroy()
    {
        foreach (var texture in _textureCache.Values)
            texture.Dispose();
        _textureCache.Clear();
        _gpuPrewarmed = false;
    }

    public void SetChargeProgress(float progress)
    {
        _chargeProgress = Math.Clamp(progress, 0f, 1f);
        var texture = chargedSprite.Texture;
        var visibleHeight = Math.Clamp(texture.Height * _chargeProgress, 0f, texture.Height);
        chargedSprite.SceneObject!.Enabled = visibleHeight >= 1f;
        chargedSprite.SourceRect = visibleHeight >= 1f
            ? new Rect(0f, texture.Height - visibleHeight, texture.Width, visibleHeight)
            : null;

        var ready = _chargeProgress >= 1f;
        foreach (var glow in glowSprites)
            glow.SceneObject!.Enabled = ready;
    }

    public bool TryGetViewportBounds(Camera camera, float aspectRatio, out Rect bounds)
    {
        bounds = default;
        if (aspectRatio <= 0f || !float.IsFinite(aspectRatio))
            return false;

        var localMinimum = baseSprite.LocalBoundsMin;
        var localMaximum = baseSprite.LocalBoundsMax;
        var world = baseSprite.Transform.WorldMatrix;
        var viewProjection = camera.ViewMatrix * camera.GetProjectionMatrix(aspectRatio);
        Span<Vector3> corners =
        [
            new(localMinimum.X, localMinimum.Y, 0f),
            new(localMaximum.X, localMinimum.Y, 0f),
            new(localMinimum.X, localMaximum.Y, 0f),
            new(localMaximum.X, localMaximum.Y, 0f)
        ];

        var minimum = new Vector2(float.PositiveInfinity);
        var maximum = new Vector2(float.NegativeInfinity);
        foreach (var localCorner in corners)
        {
            var worldCorner = Vector3.Transform(localCorner, world);
            var clip = Vector4.Transform(new Vector4(worldCorner, 1f), viewProjection);
            if (MathF.Abs(clip.W) <= float.Epsilon || !float.IsFinite(clip.W))
                return false;

            var viewport = new Vector2(
                (clip.X / clip.W + 1f) * 0.5f,
                (1f - clip.Y / clip.W) * 0.5f);
            minimum = Vector2.Min(minimum, viewport);
            maximum = Vector2.Max(maximum, viewport);
        }

        bounds = new Rect(minimum.X, minimum.Y, maximum.X - minimum.X, maximum.Y - minimum.Y);
        return bounds.Width > 0f && bounds.Height > 0f;
    }

    public override void Update(float deltaTime)
    {
        _elapsed += deltaTime;
        var bob = MathF.Sin(_elapsed * _config.BobSpeed) * _config.BobAmplitude;
        var sway = MathF.Sin(_elapsed * _config.SwaySpeed) * _config.SwayAmplitudeRadians;
        var breath = 1.0f + MathF.Sin(_elapsed * _config.BreathingSpeed) * _config.BreathingAmplitude;
        var chargeScale = _config.MinimumChargeScale +
                          (_config.MaximumChargeScale - _config.MinimumChargeScale) * SmoothStep(_chargeProgress);
        Transform.Position = _origin + Vector3.UnitY * bob;
        Transform.Rotation = Quaternion.CreateFromAxisAngle(Vector3.UnitZ, sway);
        Transform.Scale = _baseScale * new Vector3(chargeScale * breath, chargeScale * breath, 1.0f);

        if (_chargeProgress < 1f)
            return;
        var pulse = 1f + MathF.Sin(_elapsed * _config.GlowPulseSpeed) * _config.GlowPulseAmplitude;
        for (var index = 0; index < glowSprites.Count; index++)
        {
            var glow = glowSprites[index];
            var layerScale = _config.GlowScale + index * 0.08f;
            glow.Transform.Scale = new Vector3(layerScale * pulse, layerScale * pulse, 1f);
            glow.Color = new Vector4(
                _config.GlowRed,
                _config.GlowGreen,
                _config.GlowBlue,
                _config.GlowAlpha / (index + 1));
        }
    }

    private static float SmoothStep(float value) => value * value * (3f - 2f * value);
}
