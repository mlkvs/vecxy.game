using HardCore.Cultivation.Game.Infrastructure;
using Vecxy.Assets;
using Vecxy.Rendering;
using Vecxy.Scene;

namespace HardCore.Cultivation.Game;

public class Background(
    IAssetsManager assets,
    SpriteRenderer cultivationSprite,
    SpriteRenderer missionSprite) : AComponent
{
    private const float FadeDurationSeconds = 0.8f;

    public class Prototype(IAssetsManager assets) : APrototype<Background, Prototype.Options>
    {
        public class Options : IPrototype.IOptions;

        protected override Background Instantiate(InstantiateContext ctx)
        {
            if (ctx.Scene == null)
            {
                throw new NotImplementedException();
            }
            
            using var backgroundTexture = assets.Load<TextureAsset>(Assets.Textures.Background);

            var backgroundObject = ctx.Scene.CreateObject("Background");

            var background = backgroundObject.AddComponent<SpriteRenderer>();
            background.SetTexture(backgroundTexture);
            background.PixelsPerUnit = 1.0f;
            background.SortingLayer = 0;

            using var missionBackgroundTexture = assets.Load<TextureAsset>(Assets.Textures.BackgroundMissions);
            var missionBackgroundObject = backgroundObject.CreateChild("Mission background");
            var missionBackground = missionBackgroundObject.AddComponent<SpriteRenderer>();
            missionBackground.SetTexture(missionBackgroundTexture);
            missionBackground.PixelsPerUnit = 1.0f;
            missionBackground.SortingLayer = 0;
            missionBackground.OrderInLayer = 1;
            missionBackground.Color = new System.Numerics.Vector4(1f, 1f, 1f, 0f);

            return backgroundObject.AddComponent(new Background(assets, background, missionBackground));
        }

        protected override void Configure(Background component, Options options)
        {
        }
    }
    
    private bool _missionMode;
    private float _missionOpacity;
    private string _cultivationTexturePath = string.Empty;
    private string _missionTexturePath = string.Empty;

    public SpriteRenderer SpriteRenderer => cultivationSprite;

    public void SetStage(CultivationStageConfig stage)
    {
        if (!string.Equals(_cultivationTexturePath, stage.CultivationBackgroundTexture, StringComparison.Ordinal))
        {
            using var texture = assets.Load<TextureAsset>(
                new TextureHandle(assets.Find(stage.CultivationBackgroundTexture)));
            cultivationSprite.SetTexture(texture);
            _cultivationTexturePath = stage.CultivationBackgroundTexture;
        }

        if (!string.Equals(_missionTexturePath, stage.MissionBackgroundTexture, StringComparison.Ordinal))
        {
            using var texture = assets.Load<TextureAsset>(
                new TextureHandle(assets.Find(stage.MissionBackgroundTexture)));
            missionSprite.SetTexture(texture);
            _missionTexturePath = stage.MissionBackgroundTexture;
        }
    }

    public void SetMissionMode(bool missionMode)
    {
        _missionMode = missionMode;
    }

    public override void Update(float deltaTime)
    {
        var targetOpacity = _missionMode ? 1f : 0f;
        var opacityStep = Math.Max(0f, deltaTime) / FadeDurationSeconds;
        _missionOpacity = targetOpacity > _missionOpacity
            ? MathF.Min(targetOpacity, _missionOpacity + opacityStep)
            : MathF.Max(targetOpacity, _missionOpacity - opacityStep);
        missionSprite.Color = new System.Numerics.Vector4(1f, 1f, 1f, _missionOpacity);
    }
}
