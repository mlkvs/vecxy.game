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

var spreadsheetBalance = new CultivationBalanceSnapshot(
    new GameBalanceConfig
    {
        InitialCharacterStats = new CharacterStats(100m, 1m, 1m, 1m, 63m)
    },
    new CultivationConfig
    {
        InitialRequiredPower = [100m, 200m],
        Stages =
        [
            new CultivationStageConfig
            {
                Id = "body_tempering",
                RecursiveCoefficient = 0.7m,
                SpiritualPowerMultiplier = 1m,
                StatsPerLevel = new CharacterStats(5m, 0.1m, 0.5m, 0.05m, 0.5m)
            }
        ]
    });
Check(spreadsheetBalance.GetCost(0, 3) == 210m && spreadsheetBalance.GetCost(0, 10) == 1103.987003m,
    "Cultivation costs do not match the spreadsheet recurrence.");
Check(spreadsheetBalance.GetStart(0) == new CharacterStats(100m, 1m, 1m, 1m, 63m) &&
      spreadsheetBalance.GetEnd(0) == new CharacterStats(150m, 2m, 6m, 1.5m, 68m),
    "Cultivation statistics do not match the spreadsheet baseline.");
var spreadsheetQualityBalance = new GameBalanceConfig
{
    QualityPriceCurve =
    [
        new PriceCurvePoint { Quality = 1m, Multiplier = 1m },
        new PriceCurvePoint { Quality = 2m, Multiplier = 1.25m },
        new PriceCurvePoint { Quality = 3m, Multiplier = 1.75m },
        new PriceCurvePoint { Quality = 4m, Multiplier = 2.5m },
        new PriceCurvePoint { Quality = 5m, Multiplier = 3.5m }
    ],
    LowQualityPriceMultipliers =
    {
        [ItemCategory.Pill] = new PriceCurvePoint { Quality = 0.1m, Multiplier = 0.5m }
    }
};
Check(ItemBalanceFormula.GetQualityMultiplier(spreadsheetQualityBalance, ItemCategory.Pill, 0.1m) == 0.5m &&
      ItemBalanceFormula.GetQualityMultiplier(spreadsheetQualityBalance, ItemCategory.Pill, 5m) == 3.5m,
    "Item effect quality multipliers do not match the spreadsheet.");
Check(ContaminationCalculator.Combine(Enumerable.Repeat(0.2m, 6), 3m) == 0.245952m &&
      ContaminationCalculator.Combine([], 3m) == 0m &&
      ContaminationCalculator.Combine([0.2m], 3m) == 0.2m / 3m,
    "Contamination probability formula is incorrect.");

var state = new GameState(database.Balance.TicksPerYear);
state.EnqueueMission(new ActiveMission { MissionConfigId = "mission", RequiredProgress = 10 });
var cultivationWeek = processor.ProcessTick(state);
Check(cultivationWeek.SpiritualPowerGained > 0 && cultivationWeek.MissionProgressAdded == 0, "Cultivation mode must only add spiritual power.");
var powerAfterCultivation = state.Character.SpiritualPower;
state.SetActivityMode(ActivityMode.Missions);
var missionWeek = processor.ProcessTick(state);
Check(missionWeek.SpiritualPowerGained == 0 && missionWeek.MissionProgressAdded > 0, "Mission mode must only add mission progress.");
Check(state.Character.SpiritualPower == powerAfterCultivation, "Mission mode changed spiritual power.");

var combat = new CombatService(database);
var combatState = new GameState(database.Balance.TicksPerYear);
combat.ConfigureHero(combatState.Character, true);
combatState.SetActivityMode(ActivityMode.Missions);
combatState.EnqueueMission(new ActiveMission
{
    MissionConfigId = "mission",
    RequiredProgress = 10,
    Encounter = new MissionEncounter
    {
        MonsterConfigId = "training_spirit",
        BackgroundId = "forest",
        DangerLevel = 1,
        TriggerProgress = 0
    }
});
var combatEvents = new List<CombatEvent>();
for (var index = 0; index < 1200 && combatState.CurrentMission?.Encounter?.Resolved != true; index++)
    combatEvents.AddRange(combat.Update(combatState, 0.1f).Events);
Check(combatEvents.Any(value => value.Type == CombatEventType.Started), "Combat did not start at encounter progress.");
Check(combatEvents.Any(value => value.Type == CombatEventType.Victory), "Hero did not win the deterministic training combat.");
Check(combatState.CurrentMission?.Encounter?.Resolved == true, "Victory did not resolve the mission encounter.");

var alchemy = new AlchemyService(database, new AlchemyCraftRandom());
Check(AlchemyResultRankFormula.Calculate(
          3m, [2m, 4m], 0.6m, 0.4m, 1.5m, 0.3m, 2m, 0m, 0.1m, 5m) == 3m,
    "Alchemy result rank formula does not apply the weighted average and maximum component.");
Check(AlchemyResultRankFormula.Calculate(
          3m, [2m, 4m], 0.6m, 0.4m, 1.5m, 0.3m, 2m, 100m, 0.1m, 5m) == 5m,
    "Alchemy result rank formula is not clamped to one rank above the best component.");
var alchemyState = new GameState(database.Balance.TicksPerYear);
var primaryIngredient = new ItemInstance
{
    InstanceId = Guid.NewGuid(), ConfigId = "ingredient", Rarity = ItemRarity.Common, Quality = 2m
};
primaryIngredient.AddQuantity(2);
var secondaryIngredient = new ItemInstance
{
    InstanceId = Guid.NewGuid(), ConfigId = "ingredient_other", Rarity = ItemRarity.Rare, Quality = 4m
};
secondaryIngredient.AddQuantity(1);
var alchemyCore = new ItemInstance
{
    InstanceId = Guid.NewGuid(), ConfigId = "attempt", Rarity = ItemRarity.Uncommon, Quality = 3m
};
alchemyState.Inventory.Add(primaryIngredient);
alchemyState.Inventory.Add(secondaryIngredient);
alchemyState.Inventory.Add(alchemyCore);
var minimumPillPreview = alchemy.Preview(alchemyState,
    [new(alchemyCore.InstanceId, 1), new(primaryIngredient.InstanceId, 2)],
    AlchemyMode.Pill);
Check(minimumPillPreview.CanCraft && minimumPillPreview.Output?.CraftedEffects.Count > 0,
    "A core and two matching ingredients did not produce a pill.");
var pillSelection = new[]
{
    new AlchemySelection(alchemyCore.InstanceId, 1),
    new AlchemySelection(primaryIngredient.InstanceId, 3),
    new AlchemySelection(secondaryIngredient.InstanceId, 2)
};
var pillPreview = alchemy.Preview(alchemyState, pillSelection, AlchemyMode.Pill);
Check(pillPreview.CanCraft && pillPreview.Output?.CraftedEffects.Count == 1,
    "Three matching properties out of five did not produce a pill effect.");
Check(pillPreview.Output!.CraftedEffects[0].Value == 60m,
    "Property coverage or the crafted pill strength formula was not applied.");
var pillResult = alchemy.Craft(alchemyState, pillSelection, AlchemyMode.Pill);
Check(pillResult.Success && pillResult.Output is { ConfigId: "vitality_pill", Quantity: 6 } &&
      pillResult.ProducedQuantity == 6,
    "Crafted pill batch did not use the configured output type and quantity distribution.");
var craftedPill = pillResult.Output!;
Check(alchemyState.Inventory.Find(craftedPill.InstanceId) is not null,
    "Alchemy did not return the stored item instance required by popup actions.");
alchemyState.Inventory.Add(craftedPill.Copy());
var alchemyPrices = new ItemPriceCalculator(database);
var alchemyTransactions = new ShopTransactionService(alchemyPrices);
var pillSellPrice = alchemyPrices.GetSellPrice(craftedPill, alchemyState.Shop);
var moneyBeforePillSale = alchemyState.Character.Money;
var pillSale = alchemyTransactions.Sell(alchemyState, craftedPill.InstanceId);
Check(pillSale.Success && pillSale.TotalPrice == pillSellPrice &&
      alchemyState.Character.Money == moneyBeforePillSale + pillSellPrice,
    "Crafted pill could not be sold from its result popup.");
var pillUse = effectService.Use(alchemyState, craftedPill.InstanceId);
Check(pillUse.Success && alchemyState.ActiveEffects.Count > 0,
    "Crafted pill could not be used from its result popup.");

var independentPillState = new GameState(database.Balance.TicksPerYear);
var independentCore = new ItemInstance { InstanceId = Guid.NewGuid(), ConfigId = "attempt", Rarity = ItemRarity.Common, Quality = 1m };
var vitalityIngredient = new ItemInstance { InstanceId = Guid.NewGuid(), ConfigId = "ingredient", Rarity = ItemRarity.Common, Quality = 1m };
var clarityIngredient = new ItemInstance { InstanceId = Guid.NewGuid(), ConfigId = "ingredient_other", Rarity = ItemRarity.Common, Quality = 1m };
independentPillState.Inventory.Add(independentCore);
independentPillState.Inventory.Add(vitalityIngredient);
independentPillState.Inventory.Add(clarityIngredient);
var independentAlchemy = new AlchemyService(database, new BatchPropertyRandom());
var independentPillResult = independentAlchemy.Craft(independentPillState,
    [new(independentCore.InstanceId, 1), new(vitalityIngredient.InstanceId, 1), new(clarityIngredient.InstanceId, 1)],
    AlchemyMode.Pill);
Check(independentPillResult.Success && independentPillResult.Outputs.Select(item => item.ConfigId).Order().SequenceEqual(["clarity_pill", "vitality_pill"]),
    "Each pill in an alchemy batch must roll its output type independently.");

var swiftnessState = new GameState(database.Balance.TicksPerYear);
var swiftnessCore = new ItemInstance { InstanceId = Guid.NewGuid(), ConfigId = "attempt", Rarity = ItemRarity.Common, Quality = 1m };
var swiftnessIngredient = new ItemInstance { InstanceId = Guid.NewGuid(), ConfigId = "swiftness_ingredient", Rarity = ItemRarity.Common, Quality = 1m };
swiftnessIngredient.AddQuantity(1);
swiftnessState.Inventory.Add(swiftnessCore);
swiftnessState.Inventory.Add(swiftnessIngredient);
var swiftnessResult = new AlchemyService(database, new StableRandom()).Craft(swiftnessState,
    [new(swiftnessCore.InstanceId, 1), new(swiftnessIngredient.InstanceId, 2)],
    AlchemyMode.Pill);
Check(swiftnessResult.Success &&
      swiftnessResult.Output is { ConfigId: "time_acceleration_pill" } &&
      swiftnessResult.Output.CraftedEffects.Count == 2 &&
      swiftnessResult.Output.CraftedEffects.Any(effect => effect.Type == EffectType.MissionProgress) &&
      swiftnessResult.Output.CraftedEffects.Any(effect => effect.Type == EffectType.SpiritualPowerGain),
    "Time acceleration pill must grant both mission progress and spiritual power gain.");

var purificationState = new GameState(database.Balance.TicksPerYear);
purificationState.Character.AddContamination(0.4m);
var purificationPill = new ItemInstance
{
    InstanceId = Guid.NewGuid(),
    ConfigId = "purity_pill",
    Rarity = ItemRarity.Common,
    Quality = 1m,
    Contamination = 0.1m,
    PurificationPercent = 50m,
    CraftedEffects = [new ItemEffectDefinition { Type = EffectType.PurifyContamination, Operation = ModifierOperation.Flat, Value = 50m }]
};
purificationState.Inventory.Add(purificationPill);
var purificationUse = effectService.Use(purificationState, purificationPill.InstanceId);
Check(purificationUse.Success && purificationState.Character.Contamination == 0m,
    "Purification did not apply pill contamination before removing contamination.");

var failedPillState = new GameState(database.Balance.TicksPerYear);
var failedPillCore = new ItemInstance { InstanceId = Guid.NewGuid(), ConfigId = "attempt", Rarity = ItemRarity.Common, Quality = 1m };
var failedPillIngredient = new ItemInstance { InstanceId = Guid.NewGuid(), ConfigId = "ingredient", Rarity = ItemRarity.Common, Quality = 1m };
failedPillIngredient.AddQuantity(1);
failedPillState.Inventory.Add(failedPillCore);
failedPillState.Inventory.Add(failedPillIngredient);
var failedPill = new AlchemyService(database, new MaximumRandom()).Craft(failedPillState,
    [new(failedPillCore.InstanceId, 1), new(failedPillIngredient.InstanceId, 2)], AlchemyMode.Pill);
Check(!failedPill.Success && failedPill.IngredientsDestroyed && failedPill.SuccessChancePercent == 20m &&
      failedPillState.Inventory.Items.Count == 0,
    "A failed pill craft did not destroy every selected item.");

var distillationState = new GameState(database.Balance.TicksPerYear);
var rawA = new ItemInstance { InstanceId = Guid.NewGuid(), ConfigId = "ingredient", Rarity = ItemRarity.Common, Quality = 1m };
var rawB = new ItemInstance { InstanceId = Guid.NewGuid(), ConfigId = "ingredient", Rarity = ItemRarity.Rare, Quality = 2m };
var rawC = new ItemInstance { InstanceId = Guid.NewGuid(), ConfigId = "ingredient", Rarity = ItemRarity.Epic, Quality = 3m };
distillationState.Inventory.Add(rawA);
distillationState.Inventory.Add(rawB);
distillationState.Inventory.Add(rawC);
var distillationAlchemy = new AlchemyService(database, new FirstMinimumThenMaximumRandom());
var minimumRefiningPreview = distillationAlchemy.Preview(distillationState,
    [new(rawA.InstanceId, 1), new(rawB.InstanceId, 1)],
    AlchemyMode.Distillation);
Check(minimumRefiningPreview.CanCraft,
    "Two matching ingredients were rejected by refining.");
var distillation = distillationAlchemy.Craft(distillationState,
    [new(rawA.InstanceId, 1), new(rawB.InstanceId, 1), new(rawC.InstanceId, 1)],
    AlchemyMode.Distillation);
Check(distillation.Success && distillation.Output is { DistillationLevel: 1, Rarity: ItemRarity.Rare } &&
      distillation.Output.Quality > 2m,
    "Distillation did not improve quality and average rarity.");
var extract = distillation.Output!;
distillationState.Inventory.Add(extract.Copy(2));
var powerful = distillationAlchemy.Preview(distillationState,
    [new(extract.InstanceId, 3)], AlchemyMode.Distillation);
Check(powerful.CanCraft && powerful.Output is { DistillationLevel: 2 } &&
      powerful.Output.AlchemyProperties.Select(value => value.PropertyId)
          .SequenceEqual(extract.AlchemyProperties.Select(value => value.PropertyId)),
    "Repeated distillation did not preserve the extract properties.");

var failedDistillationState = new GameState(database.Balance.TicksPerYear);
var failedDistillationInput = new ItemInstance { InstanceId = Guid.NewGuid(), ConfigId = "ingredient", Rarity = ItemRarity.Common, Quality = 1m };
failedDistillationInput.AddQuantity(1);
failedDistillationState.Inventory.Add(failedDistillationInput);
var failedDistillation = new AlchemyService(database, new MaximumRandom()).Craft(failedDistillationState,
    [new(failedDistillationInput.InstanceId, 2)], AlchemyMode.Distillation);
Check(!failedDistillation.Success && failedDistillation.IngredientsDestroyed && failedDistillation.SuccessChancePercent == 10m &&
      failedDistillationState.Inventory.Items.Count == 0,
    "A failed distillation did not destroy every selected item.");

var savePath = Path.Combine(Path.GetTempPath(), $"cultivation-alchemy-{Guid.NewGuid():N}.json");
var saveSystem = new GameSaveSystem(database) { SavePath = savePath };
saveSystem.Save(distillationState);
Check(saveSystem.TryLoad(out var loadedAlchemyState) &&
      loadedAlchemyState.Inventory.Items.Any(item => item.DistillationLevel == 1 && item.AlchemyProperties.Count > 0),
    "Alchemy metadata was not preserved by save version 10.");
File.Delete(savePath);

var defeatedState = new GameState(database.Balance.TicksPerYear);
combat.ConfigureHero(defeatedState.Character, true);
defeatedState.SetActivityMode(ActivityMode.Missions);
var defeatedMission = new ActiveMission
{
    MissionConfigId = "mission",
    RequiredProgress = 10,
    Encounter = new MissionEncounter
    {
        MonsterConfigId = "training_spirit",
        BackgroundId = "forest",
        DangerLevel = 1,
        TriggerProgress = 0
    }
};
var defeatedCombat = new ActiveCombat
{
    MonsterConfigId = "training_spirit",
    BackgroundId = "forest",
    DangerLevel = 1,
    EnemyMaximumHealth = 100
};
defeatedCombat.Initialize(100, 1, 1);
defeatedCombat.Finish(CombatPhase.Defeat, 0);
defeatedMission.StartCombat(defeatedCombat);
defeatedState.EnqueueMission(defeatedMission);
_ = combat.Update(defeatedState, 0.1f);
Check(!defeatedState.RecoveryRequired && defeatedState.ActivityMode == ActivityMode.Missions,
    "Defeat must not lock missions behind mandatory recovery.");
Check(defeatedState.CurrentMission is null && defeatedState.Character.Health == 0.1m,
    "Defeat did not remove the mission and preserve survival health.");
var healthAfterDefeat = defeatedState.Character.Health;
for (var index = 0; index < 5000 && defeatedState.Character.Health < defeatedState.Character.MaximumHealth; index++)
    _ = combat.Update(defeatedState, 0.25f);
Check(defeatedState.Character.Health > healthAfterDefeat, "Health regeneration did not advance gradually.");
Check(defeatedState.Character.Health == defeatedState.Character.MaximumHealth,
    "Health did not recover fully outside combat.");

var stageHealth = new CharacterState();
stageHealth.Cultivation.Restore(0, 10, database.Cultivation.Stages.Count);
var maximumHealthBeforeBreakthrough = combat.GetHeroMaximumHealth(stageHealth);
stageHealth.Cultivation.Restore(1, 1, database.Cultivation.Stages.Count);
var maximumHealthAfterBreakthrough = combat.GetHeroMaximumHealth(stageHealth);
var expectedHealthAfterBreakthrough = database.CultivationBalance
    .GetCurrent(stageHealth.Cultivation, database.Cultivation).MaximumHealth;
Check(maximumHealthAfterBreakthrough == expectedHealthAfterBreakthrough,
    "Maximum health did not increase by the stage bonus after breakthrough.");

var leveling = new GameState(database.Balance.TicksPerYear);
leveling.Character.AddSpiritualPower(1000m);
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
var breakthroughResult = cultivation.AttemptBreakthrough(breakthrough.Character, breakthrough.ActiveEffects);
Check(breakthrough.ActiveEffects.Count == 0, "Next-attempt breakthrough effect was not consumed.");
Check(breakthroughResult.Success && breakthrough.Character.SpiritualPower == 0m,
    "Successful breakthrough did not clear accumulated spiritual power.");

var overchargedCharacter = new CharacterState();
overchargedCharacter.Cultivation.Restore(0, 10, database.Cultivation.Stages.Count);
var breakthroughPower = cultivation.GetRequiredPower(0, 10);
overchargedCharacter.AddSpiritualPower(breakthroughPower * 2m);
var overchargedChance = cultivation.GetBreakthroughChance(overchargedCharacter, []);
Check(overchargedChance == database.Cultivation.Stages[0].BaseBreakthroughChance +
      database.Cultivation.BreakthroughChancePerExtraPowerBar,
    "An extra spiritual-power bar did not increase breakthrough chance.");

var missionState = new GameState(database.Balance.TicksPerYear);
missionService.Refresh(missionState);
Check(missionState.MissionBoard.Offers.Count == 8, "Mission board must always generate eight offers.");
Check(missionState.MissionBoard.Offers.Select(offer => offer.OfferId).Distinct().Count() == 8,
    "Repeated mission templates must remain individual offers.");
Check(missionState.MissionBoard.Offers.All(offer => offer.DangerLevel == 1),
    "Each mission offer must receive its own danger level.");
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
Check(itemRewards.All(reward => reward.ItemRolls.Count == reward.Quantity),
    "Mission item rewards were not rolled individually.");

var categoryShopState = new ShopState();
shopService.Refresh(categoryShopState);
Check(categoryShopState.Slots.Count(slot => database.GetItem(slot.Item.ConfigId).Category == ItemCategory.Ingredient) == 2,
    "Shop must generate the configured number of ingredient slots.");
Check(categoryShopState.Slots.Count(slot => database.GetItem(slot.Item.ConfigId).Category is ItemCategory.Pill or ItemCategory.Core) == 2,
    "Shop must generate the configured number of pill and core slots.");

var failedCultivation = new CultivationService(database, new MaximumRandom());
var failedCharacter = new CharacterState();
failedCharacter.Cultivation.Restore(0, 10, database.Cultivation.Stages.Count);
failedCharacter.AddSpiritualPower(failedCultivation.GetRequiredPower(0, 10));
var failedResult = failedCultivation.AttemptBreakthrough(failedCharacter, []);
Check(!failedResult.Success && failedResult.LevelsLost > 0 && failedResult.Message.Contains("получили травму"), "Failed breakthrough must report injury and lost levels.");

var tapState = new GameState(database.Balance.TicksPerYear);
tapState.SetActivityMode(ActivityMode.Missions);
tapState.Character.Cultivation.Restore(1, 1, database.Cultivation.Stages.Count);
tapState.EnqueueMission(new ActiveMission { MissionConfigId = "mission", RequiredProgress = 10 });
tapState.ActiveEffects.Add(new ActiveEffect("tap", new ItemEffectDefinition
{
    Type = EffectType.SpiritualPowerGain,
    Operation = ModifierOperation.Flat,
    Value = 1
}, 1, null, ItemDurationType.Permanent, ItemRarity.Common, 1));
var tapResult = processor.ProcessTap(tapState);
Check(tapResult.SpiritualPowerGained == 200m, "Tap must ignore stage multiplier and retain item bonuses.");
Check(tapState.CurrentMission!.CurrentProgress == 0m, "Tap must not advance mission progress.");
Check(tapState.Calendar.TotalTicks == 0, "Tap must not advance calendar.");

var longevityCharacter = new CharacterState();
longevityCharacter.Cultivation.Restore(2, 1, database.Cultivation.Stages.Count);
Check(cultivation.GetMaximumAge(longevityCharacter) == database.CultivationBalance
          .GetCurrent(longevityCharacter.Cultivation, database.Cultivation).LongevityYears,
    "Stage-based longevity bonus is incorrect.");
var longevityState = new GameState(database.Balance.TicksPerYear);
longevityState.Character.Cultivation.Restore(2, 1, database.Cultivation.Stages.Count);
longevityState.Inventory.Add(new ItemInstance
{
    InstanceId = Guid.NewGuid(),
    ConfigId = "longevity_pill",
    Rarity = ItemRarity.Common,
    Quality = 1m
});
var maximumAgeBeforePill = cultivation.GetMaximumAge(longevityState.Character, longevityState.ActiveEffects);
var longevityPill = longevityState.Inventory.Items.Single(item => item.ConfigId == "longevity_pill");
var longevityUse = effectService.Use(longevityState, longevityPill.InstanceId);
Check(longevityUse.Success, "Longevity pill could not be used.");
Check(cultivation.GetMaximumAge(longevityState.Character, longevityState.ActiveEffects) == maximumAgeBeforePill + 5m,
    "Longevity pill must increase maximum age by its fixed amount.");

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
            StartingAgeYears = 16, MaximumMissionQueueSize = 6,
            QualityBands = [new QualityBand { Index = 1, Weight = 1 }],
            ContaminationBands = [new ContaminationBand { Minimum = 0m, Maximum = 0m, Weight = 1m }],
            ContaminationLevels =
            [
                new ContaminationLevelConfig { MinimumContamination = 0.25m, Name = "One" },
                new ContaminationLevelConfig { MinimumContamination = 0.5m, Name = "Two" },
                new ContaminationLevelConfig { MinimumContamination = 0.75m, Name = "Three" },
                new ContaminationLevelConfig { MinimumContamination = 1m, Name = "Four" }
            ],
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
                new ItemConfig { Id = "ingredient", Name = "Ingredient", Category = ItemCategory.Ingredient, DurationType = ItemDurationType.Instant, BasePrice = 10,
                    AlchemyProperties = [new AlchemyPropertyAmount { PropertyId = "vitality" }] },
                new ItemConfig { Id = "ingredient_other", Name = "Other", Category = ItemCategory.Ingredient, DurationType = ItemDurationType.Instant, BasePrice = 10,
                    AlchemyProperties = [new AlchemyPropertyAmount { PropertyId = "clarity" }] },
                new ItemConfig { Id = "swiftness_ingredient", Name = "Swiftness", Category = ItemCategory.Ingredient, DurationType = ItemDurationType.Instant, BasePrice = 10,
                    AlchemyProperties = [new AlchemyPropertyAmount { PropertyId = "swiftness" }] },
                new ItemConfig { Id = "vitality_pill", Name = "Vitality pill", Category = ItemCategory.Pill, DurationType = ItemDurationType.Temporary,
                    TemporaryDurationTicks = 48, BasePrice = 10, ShopWeight = 0 },
                new ItemConfig { Id = "clarity_pill", Name = "Clarity pill", Category = ItemCategory.Pill, DurationType = ItemDurationType.Temporary,
                    TemporaryDurationTicks = 48, BasePrice = 10, ShopWeight = 0 },
                new ItemConfig { Id = "purity_pill", Name = "Purity pill", Category = ItemCategory.Pill, DurationType = ItemDurationType.Instant,
                    BasePrice = 10, ShopWeight = 0 },
                new ItemConfig { Id = "time_acceleration_pill", Name = "Time pill", Category = ItemCategory.Pill, DurationType = ItemDurationType.Temporary,
                    TemporaryDurationTicks = 48, BasePrice = 10, ShopWeight = 0 },
                new ItemConfig { Id = "longevity_pill", Name = "Longevity pill", Category = ItemCategory.Pill, DurationType = ItemDurationType.Permanent,
                    BasePrice = 10, ShopWeight = 0, Effects = [new ItemEffectDefinition { Type = EffectType.LongevityYears, Operation = ModifierOperation.Flat, Value = 5m }] },
                new ItemConfig { Id = "alchemy_extract", Name = "Extract", Category = ItemCategory.Ingredient, DurationType = ItemDurationType.Instant,
                    BasePrice = 10, ShopWeight = 0 },
                new ItemConfig { Id = "attempt", Name = "Attempt", Category = ItemCategory.Core, DurationType = ItemDurationType.UntilBreakthroughAttempt, BasePrice = 10,
                    Effects = [new ItemEffectDefinition { Type = EffectType.BreakthroughChance, Operation = ModifierOperation.Flat, Value = 25 }] }
            ]
        },
        new MissionsConfig
        {
            BoardSlotCount = 8,
            Missions = [new MissionConfig { Id = "mission", StageId = "one", Name = "Mission", MinimumDurationTicks = 1, MaximumDurationTicks = 10,
                PossibleDangerLevels = [1], PossibleMonsterIds = ["training_spirit"], PossibleBackgroundIds = ["forest"],
                Reward = new MissionRewardConfig { RequiredItemCategory = ItemCategory.Ingredient, MinimumQuantity = 1, MaximumQuantity = 15, Money = 10 } }]
        },
        new CultivationConfig
        {
            InitialRequiredPower = [10m, 10m],
            Stages =
            [
                new CultivationStageConfig { Id = "one", Name = "One", BaseBreakthroughChance = 50, RecursiveCoefficient = 0.7m },
                new CultivationStageConfig { Id = "two", Name = "Two", BaseBreakthroughChance = 50, RecursiveCoefficient = 0.7m, SpiritualPowerMultiplier = 6 },
                new CultivationStageConfig { Id = "three", Name = "Three", BaseBreakthroughChance = 50, RecursiveCoefficient = 0.7m }
            ]
        },
        new ShopConfig { IngredientSlotCount = 2, PillAndCoreSlotCount = 2, MinimumQuantity = 1, MaximumQuantity = 1, MinimumBuyMarkup = 0, MaximumBuyMarkup = 0, SellAdjustmentPercent = -33 },
        alchemy: new AlchemyConfig
        {
            Enabled = true,
            MinimumIngredients = 2,
            MaximumIngredients = 5,
            DistillationQualityPerIngredient = 0.12m,
            DistillationQualityPerLevel = 0.18m,
            ElementCompatibility = Enum.GetValues<Element>().ToDictionary(
                left => left,
                _ => Enum.GetValues<Element>().ToDictionary(right => right, _ => 0m)),
            ContaminationModifierCurve =
            [
                new ContaminationCurvePoint { Contamination = 0m, Multiplier = 1m },
                new ContaminationCurvePoint { Contamination = 1m, Multiplier = 1m }
            ],
            Properties =
            [
                new AlchemyPropertyConfig { Id = "vitality", DisplayName = "Vitality", ResultPillItemId = "vitality_pill",
                    EffectType = EffectType.HealthRegeneration, Operation = ModifierOperation.AdditivePercent, BaseValue = 100m },
                new AlchemyPropertyConfig { Id = "clarity", DisplayName = "Clarity", ResultPillItemId = "clarity_pill",
                    EffectType = EffectType.TickEfficiency, Operation = ModifierOperation.AdditivePercent, BaseValue = 50m },
                new AlchemyPropertyConfig { Id = "purification", DisplayName = "Purification", ResultPillItemId = "purity_pill",
                    EffectType = EffectType.PurifyContamination, Operation = ModifierOperation.Flat, BaseValue = 0m },
                new AlchemyPropertyConfig { Id = "swiftness", DisplayName = "Time", ResultPillItemId = "time_acceleration_pill",
                    EffectType = EffectType.MissionProgress, Operation = ModifierOperation.AdditivePercent, BaseValue = 100m }
            ]
        });
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

sealed class BatchPropertyRandom : IRandomSource
{
    private int _unitRoll;
    private int _percentRoll;

    public int NextInt(int minInclusive, int maxExclusive) => minInclusive;

    public decimal NextDecimal(decimal minInclusive, decimal maxExclusive)
    {
        if (maxExclusive == 100m)
            return ++_percentRoll == 1 ? 0m : 50m;

        _unitRoll++;
        return _unitRoll switch
        {
            5 or 6 => 0m,
            11 => 0.9m,
            12 => 0m,
            _ => 0.5m
        };
    }
}

sealed class FirstMinimumThenMaximumRandom : IRandomSource
{
    private bool _firstDecimal = true;

    public int NextInt(int minInclusive, int maxExclusive) => maxExclusive - 1;

    public decimal NextDecimal(decimal minInclusive, decimal maxExclusive)
    {
        if (_firstDecimal)
        {
            _firstDecimal = false;
            return minInclusive;
        }
        return maxExclusive - 0.0001m;
    }
}

sealed class AlchemyCraftRandom : IRandomSource
{
    private int _decimalCalls;

    public int NextInt(int minInclusive, int maxExclusive) => maxExclusive - 1;

    public decimal NextDecimal(decimal minInclusive, decimal maxExclusive)
    {
        _decimalCalls++;
        return _decimalCalls == 2 ? maxExclusive - 0.0001m : minInclusive;
    }
}
