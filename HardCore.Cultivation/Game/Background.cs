using Vecxy.Assets;
using Vecxy.Rendering;
using Vecxy.Scene;

namespace HardCore.Cultivation.Game;

public class Background : AComponent
{
    public class Prototype(IAssetsManager assets) : APrototype<Background, Prototype.Options>
    {
        public class Options : IPrototype.IOptions;

        protected override Background Instantiate(InstantiateContext ctx)
        {
            if (ctx.Scene == null)
            {
                throw new NotImplementedException();
            }
            
            using var backgroundTexture = assets.Load<TextureAsset>("Textures/Background.png");

            var backgroundObject = ctx.Scene.CreateObject("Background");

            var background = backgroundObject.AddComponent<SpriteRenderer>();
            background.SetTexture(backgroundTexture);
            background.PixelsPerUnit = 1.0f;
            background.SortingLayer = 0;
            
            return backgroundObject.AddComponent<Background>();
        }

        protected override void Configure(Background component, Options options)
        {
        }
    }
    
    public SpriteRenderer SpriteRenderer => SceneObject?.GetComponent<SpriteRenderer>() ?? throw new NullReferenceException();
}