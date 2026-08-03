using Autofac;
using JetBrains.Annotations;
using Vecxy.Assets;
using Vecxy.Engine;
using Vecxy.Scene;

namespace HardCore.Cultivation;

[UsedImplicitly]
public sealed class GameLayer
(
    ISceneManager scenes,
    IConfigProvider configs,
    WorldStats worldStats,
    GameDatabase database,
    CultivationManager cultivation
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
            builder.RegisterType<WorldStats>().SingleInstance();
            builder.RegisterType<GameDatabase>().SingleInstance();
            builder.RegisterType<CharacterStats>().SingleInstance();
            builder.RegisterType<PlayerProgress>().SingleInstance();
            builder.RegisterType<Inventory>().SingleInstance();

            builder.RegisterType<ItemSystem>().SingleInstance();
            builder.RegisterType<CultivationSystem>().SingleInstance();
            builder.RegisterType<MissionSystem>().SingleInstance();
            builder.RegisterType<CraftingSystem>().SingleInstance();
            builder.RegisterType<GameSaveSystem>().SingleInstance();
            builder.RegisterType<CultivationManager>().SingleInstance();
        }
    }

    public override void OnInitialize()
    {
        using var world = configs.LoadConfig<WorldStats.Config>("Configs/WorldConfig.yaml");
        using var items = configs.LoadConfig<ItemsConfig>("Configs/ItemsConfig.yaml");
        using var missions = configs.LoadConfig<MissionsConfig>("Configs/MissionsConfig.yaml");
        using var crafting = configs.LoadConfig<CraftingConfig>("Configs/CraftingConfig.yaml");
        using var cultivationConfig =
            configs.LoadConfig<CultivationConfig>("Configs/CultivationConfig.yaml");

        worldStats.Initialize(world);
        database.Initialize(items, missions, crafting, cultivationConfig);

        scenes.LoadScene<MainScene>();
        cultivation.Initialize();
    }

    public override void OnUpdate(float deltaTime)
    {
        cultivation.Update(deltaTime);
    }

    public override void OnUnload()
    {
        cultivation.Save();
    }
}
