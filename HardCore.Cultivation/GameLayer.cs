using Autofac;
using System.Diagnostics;
using HardCore.Cultivation.Game;
using HardCore.Cultivation.Game.Application;
using HardCore.Cultivation.Game.Infrastructure;
using HardCore.Cultivation.Game.Presentation;
#if ANDROID
using HardCore.Cultivation.Platform;
#endif
using JetBrains.Annotations;
using Vecxy.Assets;
using Vecxy.Audio;
using Vecxy.Engine;
using Vecxy.Kernel;
using Vecxy.Scene;

namespace HardCore.Cultivation;

[UsedImplicitly]
public sealed class GameLayer
(
    ISceneManager scenes,
    IConfigProvider configs,
    GameDatabase database,
    GameBuildInfo buildInfo,
    GameAnalyticsInfo analyticsInfo,
    IAnalyticsService analytics,
    GameController game,
    IAudioManager audio,
    ILifetimeScope scope
) : AAppLayer
{
    private readonly Stopwatch _sessionTimer = new();
    private float _performanceElapsed;
    private int _performanceFrames;
    private float _performanceFrameMilliseconds;
    [AppLayerDef("game")]
    public sealed class Definition : ADefinition<GameLayer>
    {
        public override void RegisterGlobal(ContainerBuilder builder)
        {
            builder.RegisterType<MainScene>().AsSelf();
        }

        public override void RegisterLocal(ContainerBuilder builder)
        {
            builder.RegisterType<GameDatabase>().SingleInstance();
            builder.RegisterType<GameBuildInfo>().SingleInstance();
            builder.RegisterType<GameAnalyticsInfo>().SingleInstance();
            builder.RegisterType<AnalyticsService>().As<IAnalyticsService>().SingleInstance();
            builder.RegisterType<SystemRandomSource>().As<IRandomSource>().SingleInstance();
            builder.RegisterType<ItemGenerator>().SingleInstance();
            builder.RegisterType<ItemEffectService>().SingleInstance();
            builder.RegisterType<ItemPriceCalculator>().SingleInstance();
            builder.RegisterType<MissionService>().SingleInstance();
            builder.RegisterType<ShopService>().SingleInstance();
            builder.RegisterType<ShopTransactionService>().SingleInstance();
            builder.RegisterType<CultivationService>().SingleInstance();
            builder.RegisterType<AlchemyService>().SingleInstance();
            builder.RegisterType<CombatService>().SingleInstance();
            builder.RegisterType<CombatScenePresenter>().SingleInstance();
            builder.RegisterType<TickProcessor>().SingleInstance();
            builder.RegisterType<GameSaveSystem>().SingleInstance();
            builder.RegisterType<GameController>().SingleInstance();
        }
    }

    public override void OnInitialize()
    {
        var startupTimer = Stopwatch.StartNew();
#if ANDROID
        buildInfo.InitializeFromAssembly();
#else
        using var build = configs.LoadConfig<BuildConfig>(Assets.Configs.Build);
        buildInfo.Initialize(build.Value);
#endif
        // Analytics configuration is available both in IDE runs and in packaged Android assets.
        using var analyticsConfig = configs.LoadConfig<AnalyticsConfig>(Assets.Configs.Analytics);
        analyticsInfo.Initialize(analyticsConfig.Value);
#if ANDROID
        AppMetricaBootstrap.Activate(analyticsInfo.AppMetricaApiKey);
#endif
        AnalyticsEventExtensions.Bind(analytics);
        new AppStartedEvent(buildInfo.Version, buildInfo.VersionCode, buildInfo.Platform, "cold").Publish();
        _sessionTimer.Restart();

        using var balance = configs.LoadConfig<GameBalanceConfig>(Assets.Configs.GameBalance);
        using var rarities = configs.LoadConfig<RaritiesConfig>(Assets.Configs.Rarities);
        using var items = configs.LoadConfig<ItemsConfig>(Assets.Configs.Items);
        using var missions = configs.LoadConfig<MissionsConfig>(Assets.Configs.Missions);
        using var cultivation = configs.LoadConfig<CultivationConfig>(Assets.Configs.Cultivation);
        using var shop = configs.LoadConfig<ShopConfig>(Assets.Configs.Shop);
        using var monsters = configs.LoadConfig<MonstersConfig>(Assets.Configs.Monsters);
        using var combat = configs.LoadConfig<CombatConfig>(Assets.Configs.Combat);
        using var alchemy = configs.LoadConfig<AlchemyConfig>(Assets.Configs.Alchemy);

        database.Initialize(balance, rarities, items, missions, cultivation, shop, monsters, combat, alchemy);

        foreach (var sound in new[]
                 {
                     Assets.Sounds.UiClick,
                     Assets.Sounds.Item,
                     Assets.Sounds.Cultivate,
                     Assets.Sounds.Breakthrough,
                     Assets.Sounds.MissionComplete,
                     Assets.Sounds.Death
                 })
            audio.Preload(sound);
        audio.Preload(Assets.Musics.Main, loop: true);

        scenes.LoadScene<MainScene>();
        game.Initialize();
        Vecxy.Kernel.PlatformApplicationLifecycle.ActiveChanged += game.SetApplicationActive;
        startupTimer.Stop();
        if (startupTimer.ElapsedMilliseconds >= 3000)
            new SlowLoadingDetectedEvent("game_initialize", startupTimer.ElapsedMilliseconds, buildInfo.Platform).Publish();
    }

    public override void OnUpdate(float deltaTime)
    {
        game.Update(deltaTime);
        _performanceElapsed += deltaTime;
        _performanceFrames++;
        _performanceFrameMilliseconds += deltaTime * 1000f;
        if (_performanceElapsed < 60f)
            return;
        new PerformanceSampleEvent(_performanceFrames / Math.Max(0.001f, _performanceElapsed),
            _performanceFrameMilliseconds / Math.Max(1, _performanceFrames), GC.GetTotalMemory(false) / 1024 / 1024).Publish();
        _performanceElapsed = 0f;
        _performanceFrames = 0;
        _performanceFrameMilliseconds = 0f;
    }

    public override void OnUnload()
    {
        Vecxy.Kernel.PlatformApplicationLifecycle.ActiveChanged -= game.SetApplicationActive;
        new AppSessionEndedEvent(_sessionTimer.Elapsed.TotalSeconds, "unload").Publish();
        game.Save();
        game.Dispose();
        AnalyticsEventExtensions.Unbind();
    }
}
