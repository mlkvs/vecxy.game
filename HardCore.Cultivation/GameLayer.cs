using Autofac;
using HardCore.Cultivation.Game.Application;
using HardCore.Cultivation.Game.Infrastructure;
using HardCore.Cultivation.Game.Presentation;
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
    GameController game,
    IAudioManager audio
) : AAppLayer
{
    public sealed class Definition : ADefinition<GameLayer>
    {
        public override void RegisterGlobal(ContainerBuilder builder)
        {
            builder.RegisterType<MainScene>().AsSelf();
        }

        public override void RegisterLocal(ContainerBuilder builder)
        {
            builder.RegisterType<GameDatabase>().SingleInstance();
            builder.RegisterType<SystemRandomSource>().As<IRandomSource>().SingleInstance();
            builder.RegisterType<ItemGenerator>().SingleInstance();
            builder.RegisterType<ItemEffectService>().SingleInstance();
            builder.RegisterType<ItemPriceCalculator>().SingleInstance();
            builder.RegisterType<MissionService>().SingleInstance();
            builder.RegisterType<ShopService>().SingleInstance();
            builder.RegisterType<ShopTransactionService>().SingleInstance();
            builder.RegisterType<CultivationService>().SingleInstance();
            builder.RegisterType<TickProcessor>().SingleInstance();
            builder.RegisterType<GameSaveSystem>().SingleInstance();
            builder.RegisterType<GameController>().SingleInstance();
        }
    }

    public override void OnInitialize()
    {
        using var balance = configs.LoadConfig<GameBalanceConfig>("Configs/GameBalance.yaml");
        using var rarities = configs.LoadConfig<RaritiesConfig>("Configs/Rarities.yaml");
        using var items = configs.LoadConfig<ItemsConfig>("Configs/Items.yaml");
        using var missions = configs.LoadConfig<MissionsConfig>("Configs/Missions.yaml");
        using var cultivation = configs.LoadConfig<CultivationConfig>("Configs/Cultivation.yaml");
        using var shop = configs.LoadConfig<ShopConfig>("Configs/Shop.yaml");

        database.Initialize(balance, rarities, items, missions, cultivation, shop);

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
        audio.Play("Musics/Main.mp3", loop: true, volume: 0.3f);
        game.Initialize();
    }

    public override void OnUpdate(float deltaTime)
    {
        game.Update(deltaTime);
    }

    public override void OnUnload()
    {
        game.Save();
        game.Dispose();
    }
}
