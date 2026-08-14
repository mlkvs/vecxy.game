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
            builder.RegisterType<DogMeditationService>().SingleInstance();
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
        using var build = configs.LoadConfig<BuildConfig>("Configs/Build.yaml");
        buildInfo.Initialize(build.Value);
#endif
        // Analytics configuration is available both in IDE runs and in packaged Android assets.
        using var analyticsConfig = configs.LoadConfig<AnalyticsConfig>("Configs/Analytics.yaml");
        analyticsInfo.Initialize(analyticsConfig.Value);
#if ANDROID
        AppMetricaBootstrap.Activate(analyticsInfo.AppMetricaApiKey);
#endif
        AnalyticsEventExtensions.Bind(analytics);
        new AppStartedEvent(buildInfo.Version, buildInfo.VersionCode, buildInfo.Platform, "cold").Publish();
        _sessionTimer.Restart();

        using var balance = configs.LoadConfig<GameBalanceConfig>("Configs/GameBalance.yaml");
        using var rarities = configs.LoadConfig<RaritiesConfig>("Configs/Rarities.yaml");
        using var items = configs.LoadConfig<ItemsConfig>("Configs/Items.yaml");
        using var missions = configs.LoadConfig<MissionsConfig>("Configs/Missions.yaml");
        using var cultivation = configs.LoadConfig<CultivationConfig>("Configs/Cultivation.yaml");
        using var shop = configs.LoadConfig<ShopConfig>("Configs/Shop.yaml");
        using var monsters = configs.LoadConfig<MonstersConfig>("Configs/Monsters.yaml");
        using var combat = configs.LoadConfig<CombatConfig>("Configs/Combat.yaml");
        using var dog = configs.LoadConfig<DogConfig>("Configs/Dog.yaml");
        using var alchemy = configs.LoadConfig<AlchemyConfig>("Configs/Alchemy.yaml");

        database.Initialize(balance, rarities, items, missions, cultivation, shop, monsters, combat, dog, alchemy);

        foreach (var sound in new[]
                 {
                     "Sounds/ui-click.wav",
                     "Sounds/item.wav",
                     "Sounds/cultivate.wav",
                     "Sounds/breakthrough.wav",
                     "Sounds/mission-complete.wav",
                     "Sounds/death.wav"
                 })
            audio.Preload(sound);
        audio.Preload("Musics/Main.mp3", loop: true);

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
