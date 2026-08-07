using HardCore.Cultivation.Game.Application;
using HardCore.Cultivation.Game.Domain;
using HardCore.Cultivation.Game.Infrastructure;

var database = BuildDatabase();
var random = new StableRandom();
var generator = new ItemGenerator(database, random);
var effectService = new ItemEffectService(database);
var missionService = new MissionService(database, generator, random);
var shopService = new ShopService(database, generator, random);
var cultivation = new CultivationService(database, random);
var processor = new TickProcessor(database, effectService, missionService, shopService, cultivation);

var state = new GameState(database.Balance.TicksPerYear);
state.EnqueueMission(new ActiveMission { MissionConfigId = "mission", RequiredProgress = 10 });
var cultivationWeek = processor.ProcessTick(state);
Check(cultivationWeek.SpiritualPowerGained > 0 && cultivationWeek.MissionProgressAdded == 0, "Cultivation mode must only add spiritual power.");
var powerAfterCultivation = state.Character.SpiritualPower;
state.SetActivityMode(ActivityMode.Missions);
var missionWeek = processor.ProcessTick(state);
Check(missionWeek.SpiritualPowerGained == 0 && missionWeek.MissionProgressAdded > 0, "Mission mode must only add mission progress.");
Check(state.Character.SpiritualPower == powerAfterCultivation, "Mission mode changed spiritual power.");

var leveling = new GameState(database.Balance.TicksPerYear);
processor.ProcessTick(leveling);
Check(leveling.Character.Cultivation.Level == 10, "Automatic level advancement did not reach level 10.");

var breakthrough = new GameState(database.Balance.TicksPerYear);
breakthrough.Character.Cultivation.Restore(0, 10, database.Cultivation.Stages.Count);
breakthrough.Character.AddSpiritualPower(1000);
breakthrough.ActiveEffects.Add(new ActiveEffect("attempt", new ItemEffectDefinition
{
    Type = EffectType.BreakthroughChance,
    Operation = ModifierOperation.Flat,
    Value = 25
}, 25, null, ItemDurationType.UntilBreakthroughAttempt, ItemRarity.Common, 1));
_ = cultivation.AttemptBreakthrough(breakthrough.Character, breakthrough.ActiveEffects);
Check(breakthrough.ActiveEffects.Count == 0, "Next-attempt breakthrough effect was not consumed.");

var missionState = new GameState(database.Balance.TicksPerYear);
missionService.Refresh(missionState);
Check(missionService.Start(missionState, "mission").Success, "Mission could not be accepted.");
var rewards = missionState.CurrentMission!.Rewards;
Check(rewards.Count is 1 or 2, "Mission must contain one or two rewards.");
Check(rewards.Where(reward => reward.Type == MissionRewardType.Item).All(reward => reward.Quantity is >= 1 and <= 15), "Mission item quantity is outside allowed bounds.");

var itemRandom = new MaximumRandom();
var itemMissionService = new MissionService(database, new ItemGenerator(database, itemRandom), itemRandom);
var itemMissionState = new GameState(database.Balance.TicksPerYear);
itemMissionService.Refresh(itemMissionState);
Check(itemMissionService.Start(itemMissionState, "mission").Success, "Item-reward mission could not be accepted.");
var itemRewards = itemMissionState.CurrentMission!.Rewards;
Check(itemRewards.Count == 2 && itemRewards.All(reward => reward.Type == MissionRewardType.Item), "Two-item mission reward was not generated.");
Check(itemRewards.All(reward => reward.Quantity == 15), "Ingredient reward must allow up to 15 items.");

var failedCultivation = new CultivationService(database, new MaximumRandom());
var failedCharacter = new CharacterState();
failedCharacter.Cultivation.Restore(0, 10, database.Cultivation.Stages.Count);
failedCharacter.AddSpiritualPower(1000);
var failedResult = failedCultivation.AttemptBreakthrough(failedCharacter, []);
Check(!failedResult.Success && failedResult.LevelsLost > 0 && failedResult.Message.Contains("получили травму"), "Failed breakthrough must report injury and lost levels.");

var tapState = new GameState(database.Balance.TicksPerYear);
tapState.SetActivityMode(ActivityMode.Missions);
tapState.EnqueueMission(new ActiveMission { MissionConfigId = "mission", RequiredProgress = 10 });
tapState.ActiveEffects.Add(new ActiveEffect("tap", new ItemEffectDefinition
{
    Type = EffectType.SpiritualPowerGain,
    Operation = ModifierOperation.Flat,
    Value = 1
}, 1, null, ItemDurationType.Permanent, ItemRarity.Common, 1));
var tapResult = processor.ProcessTap(tapState);
Check(tapResult.SpiritualPowerGained > 1m, "Tap must grant spiritual power with flat bonuses applied.");
Check(tapState.CurrentMission!.CurrentProgress == 0m, "Tap must not advance mission progress.");
Check(tapState.Calendar.TotalTicks == 0, "Tap must not advance calendar.");

var longevityCharacter = new CharacterState();
longevityCharacter.Cultivation.Restore(2, 1, database.Cultivation.Stages.Count);
Check(cultivation.GetMaximumAge(longevityCharacter) == 140m, "Stage-based longevity bonus is incorrect.");

var shopState = new ShopState();
shopService.Refresh(shopState);
Check(shopState.SellAdjustmentPercent == -33, "Sell adjustment must stay fixed at -33%.");

Console.WriteLine("All cultivation gameplay checks passed.");

static void Check(bool condition, string message)
{
    if (!condition) throw new InvalidOperationException(message);
}

static GameDatabase BuildDatabase()
{
    var database = new GameDatabase();
    database.Initialize(
        new GameBalanceConfig
        {
            TicksPerYear = 48, RealMillisecondsPerTick = 1000, BaseSpiritualPowerPerTick = 100,
            StartingAgeYears = 16, MaximumAgeYears = 80, MaximumMissionQueueSize = 6,
            QualityBands = [new QualityBand { Index = 1, Weight = 1 }],
            QualityPriceCurve = [new PriceCurvePoint { Quality = 0.1m, Multiplier = 1 }, new PriceCurvePoint { Quality = 5, Multiplier = 1 }]
        },
        new RaritiesConfig
        {
            Rarities = Enum.GetValues<ItemRarity>().Select(rarity => new RarityConfig { Rarity = rarity, DisplayName = rarity.ToString(), ShopWeight = 1 }).ToList()
        },
        new ItemsConfig
        {
            Items =
            [
                new ItemConfig { Id = "ingredient", Name = "Ingredient", Category = ItemCategory.Ingredient, DurationType = ItemDurationType.Instant, BasePrice = 10 },
                new ItemConfig { Id = "attempt", Name = "Attempt", Category = ItemCategory.Core, DurationType = ItemDurationType.UntilBreakthroughAttempt, BasePrice = 10,
                    Effects = [new ItemEffectDefinition { Type = EffectType.BreakthroughChance, Operation = ModifierOperation.Flat, Value = 25 }] }
            ]
        },
        new MissionsConfig
        {
            BoardSlotCount = 1,
            Missions = [new MissionConfig { Id = "mission", Name = "Mission", MinimumDurationTicks = 1, MaximumDurationTicks = 10,
                Reward = new MissionRewardConfig { RequiredItemCategory = ItemCategory.Ingredient, Money = 10 } }]
        },
        new CultivationConfig
        {
            BaseRequiredPower = 10, LevelMultipliers = Enumerable.Repeat(1m, 10).ToList(),
            Stages =
            [
                new CultivationStageConfig { Id = "one", Name = "One", BaseBreakthroughChance = 50 },
                new CultivationStageConfig { Id = "two", Name = "Two", BaseBreakthroughChance = 50 }
            ]
        },
        new ShopConfig { SlotCount = 2, MinimumQuantity = 1, MaximumQuantity = 2, MinimumBuyMarkup = 0, MaximumBuyMarkup = 0, SellAdjustmentPercent = -33 });
    return database;
}

sealed class StableRandom : IRandomSource
{
    public int NextInt(int minInclusive, int maxExclusive) => minInclusive;
    public decimal NextDecimal(decimal minInclusive, decimal maxExclusive) => minInclusive;
}

sealed class MaximumRandom : IRandomSource
{
    public int NextInt(int minInclusive, int maxExclusive) => maxExclusive - 1;
    public decimal NextDecimal(decimal minInclusive, decimal maxExclusive) => maxExclusive - 0.0001m;
}
