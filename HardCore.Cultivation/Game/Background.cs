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
    private string? _stageId;
    private Task _stageLoadTask = Task.CompletedTask;
    private CancellationTokenSource? _stageLoadCancellation;
    private AssetPackageLease? _stagePackage;

    public SpriteRenderer SpriteRenderer => cultivationSprite;

    public Task SetStageAsync(CultivationStageConfig stage)
    {
        if (string.Equals(_stageId, stage.Id, StringComparison.Ordinal))
            return _stageLoadTask;

        _stageId = stage.Id;
        _stageLoadCancellation?.Cancel();
        _stageLoadCancellation?.Dispose();
        _stageLoadCancellation = new CancellationTokenSource();
        _stageLoadTask = LoadStageAndResetOnFailureAsync(stage.Id, _stageLoadCancellation.Token);
        return _stageLoadTask;
    }

    private async Task LoadStageAndResetOnFailureAsync(string stageId, CancellationToken cancellationToken)
    {
        try
        {
            await LoadStageAsync(stageId, cancellationToken);
        }
        catch
        {
            if (string.Equals(_stageId, stageId, StringComparison.Ordinal))
                _stageId = null;
            throw;
        }
    }

    private async Task LoadStageAsync(string stageId, CancellationToken cancellationToken)
    {
        var stage = ResolveStageAssets(stageId);
        var package = await stage.EnsureLoaded(cancellationToken);
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            using var cultivationTexture = assets.Load<TextureAsset>(stage.Cultivation);
            using var missionTexture = assets.Load<TextureAsset>(stage.Missions);
            cultivationSprite.SetTexture(cultivationTexture);
            missionSprite.SetTexture(missionTexture);

            var previousPackage = _stagePackage;
            _stagePackage = package;
            previousPackage?.Dispose();
        }
        catch
        {
            package.Dispose();
            throw;
        }
    }

    // Stage ids belong to gameplay configuration; asset identities remain generated,
    // typed handles. Adding a stage therefore produces a compile-time-visible mapping.
    private static StageAssets ResolveStageAssets(string stageId) => stageId switch
    {
        "body_tempering" => new(Assets.BodyTemperingBackgrounds.Textures.Cultivation,
            Assets.BodyTemperingBackgrounds.Textures.Missions,
            token => Assets.BodyTemperingBackgrounds.EnsureLoadedAsync(cancellationToken: token)),
        "qi_gathering" => new(Assets.QiGatheringBackgrounds.Textures.Cultivation,
            Assets.QiGatheringBackgrounds.Textures.Missions,
            token => Assets.QiGatheringBackgrounds.EnsureLoadedAsync(cancellationToken: token)),
        "golden_core" => new(Assets.GoldenCoreBackgrounds.Textures.Cultivation,
            Assets.GoldenCoreBackgrounds.Textures.Missions,
            token => Assets.GoldenCoreBackgrounds.EnsureLoadedAsync(cancellationToken: token)),
        "nascent_soul" => new(Assets.NascentSoulBackgrounds.Textures.Cultivation,
            Assets.NascentSoulBackgrounds.Textures.Missions,
            token => Assets.NascentSoulBackgrounds.EnsureLoadedAsync(cancellationToken: token)),
        "soul_formation" => new(Assets.SoulFormationBackgrounds.Textures.Cultivation,
            Assets.SoulFormationBackgrounds.Textures.Missions,
            token => Assets.SoulFormationBackgrounds.EnsureLoadedAsync(cancellationToken: token)),
        "void_refinement" => new(Assets.VoidRefinementBackgrounds.Textures.Cultivation,
            Assets.VoidRefinementBackgrounds.Textures.Missions,
            token => Assets.VoidRefinementBackgrounds.EnsureLoadedAsync(cancellationToken: token)),
        "immortal_ascension" => new(Assets.ImmortalAscensionBackgrounds.Textures.Cultivation,
            Assets.ImmortalAscensionBackgrounds.Textures.Missions,
            token => Assets.ImmortalAscensionBackgrounds.EnsureLoadedAsync(cancellationToken: token)),
        _ => throw new InvalidDataException($"Cultivation stage '{stageId}' has no typed background package mapping.")
    };

    public override void OnDestroy()
    {
        _stageLoadCancellation?.Cancel();
        _stageLoadCancellation?.Dispose();
        _stagePackage?.Dispose();
    }

    private readonly record struct StageAssets(
        TextureHandle Cultivation,
        TextureHandle Missions,
        Func<CancellationToken, ValueTask<AssetPackageLease>> EnsureLoaded);

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
