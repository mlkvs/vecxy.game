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
var dogMeditation = new DogMeditationService(database, random);
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
for (var index = 0; index < 200 && combatState.CurrentMission?.Encounter?.Resolved != true; index++)
    combatEvents.AddRange(combat.Update(combatState, 0.1f).Events);
Check(combatEvents.Any(value => value.Type == CombatEventType.Started), "Combat did not start at encounter progress.");
Check(combatEvents.Any(value => value.Type == CombatEventType.Victory), "Hero did not win the deterministic training combat.");
Check(combatState.CurrentMission?.Encounter?.Resolved == true, "Victory did not resolve the mission encounter.");

var dogState = new GameState(database.Balance.TicksPerYear);
Check(!dogMeditation.Update(dogState, 0.5f), "Dog meditation became ready too early.");
Check(!dogMeditation.Collect(dogState).Success, "Dog reward was collected before charging.");
Check(dogMeditation.Update(dogState, 0.5f), "Dog meditation did not become ready.");
var dogReward = dogMeditation.Collect(dogState);
Check(dogReward.Success && dogReward.Reward == 1000 && dogState.Character.Money == 1000,
    "Dog meditation reward is incorrect.");
Check(dogMeditation.GetProgress(dogState) == 0f, "Dog meditation did not reset after collection.");
var maximumDogMeditation = new DogMeditationService(database, new MaximumRandom());
var maximumDogState = new GameState(database.Balance.TicksPerYear);
_ = maximumDogMeditation.Update(maximumDogState, 1f);
Check(maximumDogMeditation.Collect(maximumDogState).Reward == 3000,
    "Dog meditation did not include the configured maximum reward.");

var alchemy = new AlchemyService(database, new MaximumRandom());
Check(AlchemyCharacteristicFormula.Calculate(1m, 1, 8m) == 9m &&
      AlchemyCharacteristicFormula.Calculate(3m, 2, 8m) == 102m &&
      AlchemyCharacteristicFormula.Calculate(5m, 5, 8m) == 1025m,
    "Alchemy characteristic formula does not match the balance table.");
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
Check(pillPreview.Output!.CraftedEffects[0].Value == 35055m,
    "Ingredient count, quality, or missing-property coverage was not applied to the crafted pill.");
var pillResult = alchemy.Craft(alchemyState, pillSelection, AlchemyMode.Pill);
Check(pillResult.Success && pillResult.Output is { CraftedDurationTicks: 48 },
    "Crafted pill was not added with its dynamic duration.");
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

var distillationState = new GameState(database.Balance.TicksPerYear);
var rawA = new ItemInstance { InstanceId = Guid.NewGuid(), ConfigId = "ingredient", Rarity = ItemRarity.Common, Quality = 1m };
var rawB = new ItemInstance { InstanceId = Guid.NewGuid(), ConfigId = "ingredient", Rarity = ItemRarity.Rare, Quality = 2m };
var rawC = new ItemInstance { InstanceId = Guid.NewGuid(), ConfigId = "ingredient", Rarity = ItemRarity.Epic, Quality = 3m };
distillationState.Inventory.Add(rawA);
distillationState.Inventory.Add(rawB);
distillationState.Inventory.Add(rawC);
var minimumRefiningPreview = alchemy.Preview(distillationState,
    [new(rawA.InstanceId, 1), new(rawB.InstanceId, 1)],
    AlchemyMode.Distillation);
Check(minimumRefiningPreview.CanCraft,
    "Two matching ingredients were rejected by refining.");
var distillation = alchemy.Craft(distillationState,
    [new(rawA.InstanceId, 1), new(rawB.InstanceId, 1), new(rawC.InstanceId, 1)],
    AlchemyMode.Distillation);
Check(distillation.Success && distillation.Output is { DistillationLevel: 1, Rarity: ItemRarity.Rare } &&
      distillation.Output.Quality > 2m,
    "Distillation did not improve quality and average rarity.");
var extract = distillation.Output!;
distillationState.Inventory.Add(extract.Copy(2));
var powerful = alchemy.Preview(distillationState,
    [new(extract.InstanceId, 3)], AlchemyMode.Distillation);
Check(powerful.CanCraft && powerful.Output is { DistillationLevel: 2 } &&
      powerful.Output.AlchemyProperties[0].Potency > extract.AlchemyProperties[0].Potency,
    "Repeated distillation did not create a more powerful extract.");

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
Check(defeatedState.RecoveryRequired, "Defeat did not enable mandatory recovery.");
Check(defeatedState.ActivityMode == ActivityMode.Cultivation, "Defeat did not switch to cultivation.");
Check(database.Combat.RecoveryHealthPoints == 30m,
    "Defeat recovery threshold must use the configured fixed HP amount.");
defeatedState.SetActivityMode(ActivityMode.Missions);
Check(defeatedState.ActivityMode == ActivityMode.Cultivation, "Missions were enabled before full recovery.");
var healthAfterDefeat = defeatedState.Character.Health;
for (var index = 0; index < 5000 && defeatedState.RecoveryRequired; index++)
    _ = combat.Update(defeatedState, 0.25f);
Check(defeatedState.Character.Health > healthAfterDefeat, "Health regeneration did not advance gradually.");
Check(!defeatedState.RecoveryRequired &&
      defeatedState.Character.Health >= database.Combat.RecoveryHealthPoints &&
      defeatedState.Character.Health < defeatedState.Character.MaximumHealth,
    "Recovery did not complete at the configured partial-health threshold.");
defeatedState.SetActivityMode(ActivityMode.Missions);
Check(defeatedState.ActivityMode == ActivityMode.Missions, "Missions stayed locked after reaching the recovery threshold.");

var stageHealth = new CharacterState();
stageHealth.Cultivation.Restore(0, 10, database.Cultivation.Stages.Count);
var maximumHealthBeforeBreakthrough = combat.GetHeroMaximumHealth(stageHealth);
stageHealth.Cultivation.Restore(1, 1, database.Cultivation.Stages.Count);
var maximumHealthAfterBreakthrough = combat.GetHeroMaximumHealth(stageHealth);
Check(maximumHealthAfterBreakthrough == maximumHealthBeforeBreakthrough + database.Combat.HeroHealthPerStage,
    "Maximum health did not increase by the stage bonus after breakthrough.");

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
                    AlchemyProperties = [new AlchemyPropertyAmount { PropertyId = "vitality", Potency = 1m }] },
                new ItemConfig { Id = "ingredient_other", Name = "Other", Category = ItemCategory.Ingredient, DurationType = ItemDurationType.Instant, BasePrice = 10,
                    AlchemyProperties = [new AlchemyPropertyAmount { PropertyId = "clarity", Potency = 1m }] },
                new ItemConfig { Id = "crafted_alchemy_pill", Name = "Pill", Category = ItemCategory.Pill, DurationType = ItemDurationType.Temporary,
                    TemporaryDurationTicks = 48, BasePrice = 10, ShopWeight = 0 },
                new ItemConfig { Id = "alchemy_extract", Name = "Extract", Category = ItemCategory.Ingredient, DurationType = ItemDurationType.Instant,
                    BasePrice = 10, ShopWeight = 0 },
                new ItemConfig { Id = "attempt", Name = "Attempt", Category = ItemCategory.Core, DurationType = ItemDurationType.UntilBreakthroughAttempt, BasePrice = 10,
                    Effects = [new ItemEffectDefinition { Type = EffectType.BreakthroughChance, Operation = ModifierOperation.Flat, Value = 25 }] }
            ]
        },
        new MissionsConfig
        {
            BoardSlotCount = 1,
            Missions = [new MissionConfig { Id = "mission", Name = "Mission", MinimumDurationTicks = 1, MaximumDurationTicks = 10,
                Reward = new MissionRewardConfig { RequiredItemCategory = ItemCategory.Ingredient, MinimumQuantity = 1, MaximumQuantity = 15, Money = 10 } }]
        },
        new CultivationConfig
        {
            InitialRequiredPower = Enumerable.Repeat(10m, 10).ToList(),
            Stages =
            [
                new CultivationStageConfig { Id = "one", Name = "One", BaseBreakthroughChance = 50 },
                new CultivationStageConfig { Id = "two", Name = "Two", BaseBreakthroughChance = 50, SpiritualPowerMultiplier = 6 },
                new CultivationStageConfig { Id = "three", Name = "Three", BaseBreakthroughChance = 50 }
            ]
        },
        new ShopConfig { SlotCount = 2, MinimumQuantity = 1, MaximumQuantity = 1, MinimumBuyMarkup = 0, MaximumBuyMarkup = 0, SellAdjustmentPercent = -33 },
        dog: new DogConfig
        {
            ChargeDurationSeconds = 1f,
            RewardUnitRubles = 1000,
            MinimumRewardUnits = 1,
            MaximumRewardUnits = 3
        },
        alchemy: new AlchemyConfig
        {
            Enabled = true,
            MinimumIngredients = 2,
            MaximumIngredients = 5,
            MinimumPropertyMatches = 2,
            MinimumPropertyFraction = 0.6m,
            MaximumPillEffects = 4,
            PillDurationTicks = 48,
            DistillationQualityPerIngredient = 0.12m,
            DistillationQualityPerLevel = 0.18m,
            DistillationPotencyPerLevel = 0.12m,
            Properties =
            [
                new AlchemyPropertyConfig { Id = "vitality", DisplayName = "Vitality", PillName = "Vitality pill",
                    EffectType = EffectType.HealthRegeneration, Operation = ModifierOperation.AdditivePercent, BaseValue = 100m },
                new AlchemyPropertyConfig { Id = "clarity", DisplayName = "Clarity", PillName = "Clarity pill",
                    EffectType = EffectType.TickEfficiency, Operation = ModifierOperation.AdditivePercent, BaseValue = 50m }
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
