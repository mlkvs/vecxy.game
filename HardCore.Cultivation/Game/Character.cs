using System.Numerics;
using JetBrains.Annotations;
using Vecxy.Assets;
using Vecxy.Rendering;
using Vecxy.Scene;

namespace HardCore.Cultivation.Game;

public class Character : AComponent
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

            // The companion is parented to the cultivator so both share the
            // same levitation transform.
            var dogObject = characterObject.CreateChild("Dog companion");
            dogObject.Transform.Position = new Vector3(-450.0f, 50.0f, 0.0f);
            dogObject.Transform.Scale = new Vector3(0.28f, 0.28f, 1.0f);
            using var dogTexture = assets.Load<TextureAsset>("Textures/Dog.png");
            var dogSprite = dogObject.AddComponent<SpriteRenderer>();
            dogSprite.SetTexture(dogTexture);
            dogSprite.PixelsPerUnit = 1.0f;
            dogSprite.Pivot = new Vector2(0.5f, 0.0f);
            dogSprite.SortingLayer = 2;
            dogObject.AddComponent(new DogCompanion(assets));
                
            // Character
            var character = characterObject.AddComponent<Character>();
                
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

    public float Amplitude { get; set; } = 24.0f;
    public float PeriodSeconds { get; set; } = 3.6f;

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

public sealed class DogCompanion(IAssetsManager assets) : AComponent
{
    private const float ExcitedDurationSeconds = 0.9f;
    private float _excitedRemaining;
    private bool _isExcited;
    private float _elapsed;
    private Vector3 _origin;
    private Vector3 _baseScale;

    public override void Start()
    {
        _origin = Transform.Position;
        _baseScale = Transform.Scale;
    }

    public void React()
    {
        _excitedRemaining = ExcitedDurationSeconds;
        if (_isExcited)
            return;
        _isExcited = true;
        SetTexture("Textures/Dog2.png");
    }

    public override void Update(float deltaTime)
    {
        _elapsed += deltaTime;
        var excitement = _isExcited ? 2.2f : 1.0f;
        var bob = MathF.Sin(_elapsed * 3.1f * excitement) * (_isExcited ? 10.0f : 5.0f);
        var sway = MathF.Sin(_elapsed * 1.8f * excitement) * (_isExcited ? 0.045f : 0.022f);
        var breath = 1.0f + MathF.Sin(_elapsed * 2.2f) * 0.012f;
        Transform.Position = _origin + Vector3.UnitY * bob;
        Transform.Rotation = Quaternion.CreateFromAxisAngle(Vector3.UnitZ, sway);
        Transform.Scale = _baseScale * new Vector3(breath, breath, 1.0f);

        if (!_isExcited)
            return;
        _excitedRemaining -= deltaTime;
        if (_excitedRemaining > 0f)
            return;
        _isExcited = false;
        SetTexture("Textures/Dog.png");
    }

    private void SetTexture(string path)
    {
        using var texture = assets.Load<TextureAsset>(path);
        SceneObject!.GetComponent<SpriteRenderer>()!.SetTexture(texture);
    }
}
