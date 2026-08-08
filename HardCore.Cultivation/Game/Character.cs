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
