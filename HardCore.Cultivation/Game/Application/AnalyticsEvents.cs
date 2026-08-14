namespace HardCore.Cultivation.Game.Application;

// Every analytics payload is explicit so gameplay code cannot silently change an event schema.
public sealed class AppStartedEvent(string appVersion, int build, string platform, string launchType) : AnalyticsEvent
{
    public override string Name => "app_started";
    public override IReadOnlyDictionary<string, object?> Parameters => CreateParameters(
        ("app_version", appVersion), ("build", build), ("platform", platform), ("launch_type", launchType));
}

public sealed class AppSessionEndedEvent(double durationSeconds, string reason) : AnalyticsEvent
{
    public override string Name => "app_session_ended";
    public override IReadOnlyDictionary<string, object?> Parameters => CreateParameters(("duration_sec", durationSeconds), ("reason", reason));
}

public sealed class FirstLaunchEvent(string platform, string appVersion) : AnalyticsEvent
{
    public override string Name => "first_launch";
    public override IReadOnlyDictionary<string, object?> Parameters => CreateParameters(("platform", platform), ("app_version", appVersion));
}

public sealed class ScreenViewEvent(string screen) : AnalyticsEvent
{
    public override string Name => "screen_view";
    public override IReadOnlyDictionary<string, object?> Parameters => CreateParameters(("screen", screen));
}

public sealed class UiActionEvent(string screen, string control, string value) : AnalyticsEvent
{
    public override string Name => "ui_action";
    public override IReadOnlyDictionary<string, object?> Parameters => CreateParameters(("screen", screen), ("control", control), ("value", value));
}

public sealed class TapBatchEvent(int count, decimal spiritualPower, int stage) : AnalyticsEvent
{
    public override string Name => "tap_batch";
    public override IReadOnlyDictionary<string, object?> Parameters => CreateParameters(("count", count), ("spiritual_power", spiritualPower), ("stage", stage));
}

public sealed class SpiritualPowerGainedEvent(string source, decimal amount, int occurrences, int stage) : AnalyticsEvent
{
    public override string Name => "spiritual_power_gained";
    public override IReadOnlyDictionary<string, object?> Parameters => CreateParameters(
        ("source", source), ("amount", amount), ("occurrences", occurrences), ("stage", stage));
}

public sealed class CultivationLevelGainedEvent(string source, int levels, int stage, int level) : AnalyticsEvent
{
    public override string Name => "cultivation_level_gained";
    public override IReadOnlyDictionary<string, object?> Parameters => CreateParameters(
        ("source", source), ("levels", levels), ("stage", stage), ("level", level));
}

public sealed class BreakthroughAttemptedEvent(int fromStage, decimal chance) : AnalyticsEvent
{
    public override string Name => "breakthrough_attempted";
    public override IReadOnlyDictionary<string, object?> Parameters => CreateParameters(("from_stage", fromStage), ("chance", chance));
}

public sealed class BreakthroughSucceededEvent(int fromStage, int toStage, decimal chance, int levelsLost) : AnalyticsEvent
{
    public override string Name => "breakthrough_succeeded";
    public override IReadOnlyDictionary<string, object?> Parameters => CreateParameters(
        ("from_stage", fromStage), ("to_stage", toStage), ("chance", chance), ("lost_levels", levelsLost));
}

public sealed class BreakthroughFailedEvent(int fromStage, int toStage, decimal chance, int levelsLost) : AnalyticsEvent
{
    public override string Name => "breakthrough_failed";
    public override IReadOnlyDictionary<string, object?> Parameters => CreateParameters(
        ("from_stage", fromStage), ("to_stage", toStage), ("chance", chance), ("lost_levels", levelsLost));
}

public sealed class CharacterDiedEvent(decimal age, int stage) : AnalyticsEvent
{
    public override string Name => "character_died";
    public override IReadOnlyDictionary<string, object?> Parameters => CreateParameters(("age", age), ("stage", stage));
}

public sealed class ContaminationChangedEvent(decimal before, decimal after, string source, int level) : AnalyticsEvent
{
    public override string Name => "contamination_changed";
    public override IReadOnlyDictionary<string, object?> Parameters => CreateParameters(
        ("before", before), ("after", after), ("source", source), ("level", level));
}

public sealed class ContaminationLevelChangedEvent(int beforeLevel, int afterLevel, decimal contamination) : AnalyticsEvent
{
    public override string Name => "contamination_level_changed";
    public override IReadOnlyDictionary<string, object?> Parameters => CreateParameters(
        ("before_level", beforeLevel), ("after_level", afterLevel), ("contamination", contamination));
}

public sealed class EffectAddedEvent(string effectId, int? durationTicks, string sourceItemId) : AnalyticsEvent
{
    public override string Name => "effect_added";
    public override IReadOnlyDictionary<string, object?> Parameters => CreateParameters(
        ("effect_id", effectId), ("duration_ticks", durationTicks), ("source_item_id", sourceItemId));
}

public sealed class EffectRemovedEvent(string effectId, string reason, string sourceItemId) : AnalyticsEvent
{
    public override string Name => "effect_removed";
    public override IReadOnlyDictionary<string, object?> Parameters => CreateParameters(
        ("effect_id", effectId), ("reason", reason), ("source_item_id", sourceItemId));
}

public sealed class ItemReceivedEvent(string itemId, int quantity, string source, decimal? contamination) : AnalyticsEvent
{
    public override string Name => "item_received";
    public override IReadOnlyDictionary<string, object?> Parameters => CreateParameters(
        ("item_id", itemId), ("quantity", quantity), ("source", source), ("contamination", contamination));
}

public sealed class PillConsumedEvent(string itemId, string category, decimal contamination) : AnalyticsEvent
{
    public override string Name => "pill_consumed";
    public override IReadOnlyDictionary<string, object?> Parameters => CreateParameters(
        ("item_id", itemId), ("category", category), ("contamination", contamination));
}

public sealed class ItemUseFailedEvent(string itemId, string category, decimal contamination, string reason) : AnalyticsEvent
{
    public override string Name => "item_use_failed";
    public override IReadOnlyDictionary<string, object?> Parameters => CreateParameters(
        ("item_id", itemId), ("category", category), ("contamination", contamination), ("reason", reason));
}

public sealed class PurificationAppliedEvent(decimal before, decimal after, decimal purifiedPercent) : AnalyticsEvent
{
    public override string Name => "purification_applied";
    public override IReadOnlyDictionary<string, object?> Parameters => CreateParameters(
        ("before", before), ("after", after), ("purified_percent", purifiedPercent));
}

public sealed class ShopOpenedEvent(long money, int stage, int itemsCount) : AnalyticsEvent
{
    public override string Name => "shop_opened";
    public override IReadOnlyDictionary<string, object?> Parameters => CreateParameters(
        ("money", money), ("stage", stage), ("items_count", itemsCount));
}

public sealed class ShopPurchaseSucceededEvent(string itemId, long price, long moneyBefore, long moneyAfter) : AnalyticsEvent
{
    public override string Name => "shop_purchase_succeeded";
    public override IReadOnlyDictionary<string, object?> Parameters => CreateParameters(
        ("item_id", itemId), ("price", price), ("money_before", moneyBefore), ("money_after", moneyAfter));
}

public sealed class ShopPurchaseFailedEvent(string itemId, long price, long money, string reason) : AnalyticsEvent
{
    public override string Name => "shop_purchase_failed";
    public override IReadOnlyDictionary<string, object?> Parameters => CreateParameters(
        ("item_id", itemId), ("price", price), ("money", money), ("reason", reason));
}

public sealed class ShopSaleSucceededEvent(string itemId, long price) : AnalyticsEvent
{
    public override string Name => "shop_sale_succeeded";
    public override IReadOnlyDictionary<string, object?> Parameters => CreateParameters(("item_id", itemId), ("price", price));
}

public sealed class ShopSaleFailedEvent(string itemId, string reason) : AnalyticsEvent
{
    public override string Name => "shop_sale_failed";
    public override IReadOnlyDictionary<string, object?> Parameters => CreateParameters(("item_id", itemId), ("reason", reason));
}

public sealed class AlchemyOpenedEvent(int inventoryIngredients) : AnalyticsEvent
{
    public override string Name => "alchemy_opened";
    public override IReadOnlyDictionary<string, object?> Parameters => CreateParameters(("inventory_ingredients", inventoryIngredients));
}

public sealed class AlchemyCraftAttemptedEvent(int ingredientsCount, string mode) : AnalyticsEvent
{
    public override string Name => "alchemy_craft_attempted";
    public override IReadOnlyDictionary<string, object?> Parameters => CreateParameters(("ingredients_count", ingredientsCount), ("mode", mode));
}

public sealed class AlchemyCraftFailedEvent(string reason, string mode) : AnalyticsEvent
{
    public override string Name => "alchemy_craft_failed";
    public override IReadOnlyDictionary<string, object?> Parameters => CreateParameters(("reason", reason), ("mode", mode));
}

public sealed class AlchemyCraftSucceededEvent(string resultItemId, decimal resultContamination, string mode, int ingredientsCount) : AnalyticsEvent
{
    public override string Name => "alchemy_craft_succeeded";
    public override IReadOnlyDictionary<string, object?> Parameters => CreateParameters(
        ("result_item_id", resultItemId), ("result_contamination", resultContamination),
        ("mode", mode), ("ingredients_count", ingredientsCount));
}

public sealed class AlchemyCraftAlternateResultEvent(string resultItemId, string expectedItemId) : AnalyticsEvent
{
    public override string Name => "alchemy_craft_alternate_result";
    public override IReadOnlyDictionary<string, object?> Parameters => CreateParameters(
        ("result_item_id", resultItemId), ("expected_item_id", expectedItemId));
}

public sealed class MissionsOpenedEvent(int stage, int availableCount) : AnalyticsEvent
{
    public override string Name => "missions_opened";
    public override IReadOnlyDictionary<string, object?> Parameters => CreateParameters(("stage", stage), ("available_count", availableCount));
}

public sealed class MissionStartedEvent(string missionId) : AnalyticsEvent
{
    public override string Name => "mission_started";
    public override IReadOnlyDictionary<string, object?> Parameters => CreateParameters(("mission_id", missionId));
}

public sealed class MissionStartFailedEvent(string missionId, string reason) : AnalyticsEvent
{
    public override string Name => "mission_start_failed";
    public override IReadOnlyDictionary<string, object?> Parameters => CreateParameters(("mission_id", missionId), ("reason", reason));
}

public sealed class MissionCompletedEvent(string? missionId, string result, decimal healthAfter) : AnalyticsEvent
{
    public override string Name => "mission_completed";
    public override IReadOnlyDictionary<string, object?> Parameters => CreateParameters(
        ("mission_id", missionId), ("result", result), ("health_after", healthAfter));
}

public sealed class MissionRewardReceivedEvent(string? missionId, long moneyTotal) : AnalyticsEvent
{
    public override string Name => "mission_reward_received";
    public override IReadOnlyDictionary<string, object?> Parameters => CreateParameters(("mission_id", missionId), ("money_total", moneyTotal));
}

public sealed class CombatStartedEvent(string? missionId, string? enemyId, decimal playerHealth, decimal? enemyHealth) : AnalyticsEvent
{
    public override string Name => "combat_started";
    public override IReadOnlyDictionary<string, object?> Parameters => CreateParameters(
        ("mission_id", missionId), ("enemy_id", enemyId), ("player_health", playerHealth), ("enemy_health", enemyHealth));
}

public sealed class CombatCompletedEvent(string result, string? missionId, string? enemyId, decimal playerHealthAfter) : AnalyticsEvent
{
    public override string Name => "combat_completed";
    public override IReadOnlyDictionary<string, object?> Parameters => CreateParameters(
        ("result", result), ("mission_id", missionId), ("enemy_id", enemyId), ("player_health_after", playerHealthAfter));
}

public sealed class CombatDefeatEvent(string? missionId, string? enemyId, int playerStage) : AnalyticsEvent
{
    public override string Name => "combat_defeat";
    public override IReadOnlyDictionary<string, object?> Parameters => CreateParameters(
        ("mission_id", missionId), ("enemy_id", enemyId), ("player_stage", playerStage));
}

public sealed class CombatDamageBatchEvent(string source, decimal totalDamage, int hits) : AnalyticsEvent
{
    public override string Name => "combat_damage_batch";
    public override IReadOnlyDictionary<string, object?> Parameters => CreateParameters(
        ("source", source), ("total_damage", totalDamage), ("hits", hits));
}

public sealed class DogMeditationOpenedEvent(bool available) : AnalyticsEvent
{
    public override string Name => "dog_meditation_opened";
    public override IReadOnlyDictionary<string, object?> Parameters => CreateParameters(("available", available));
}

public sealed class SettingsOpenedEvent(string appVersion, int build) : AnalyticsEvent
{
    public override string Name => "settings_opened";
    public override IReadOnlyDictionary<string, object?> Parameters => CreateParameters(("app_version", appVersion), ("build", build));
}

public sealed class MusicSettingChangedEvent(bool enabled, bool previousValue) : AnalyticsEvent
{
    public override string Name => "music_setting_changed";
    public override IReadOnlyDictionary<string, object?> Parameters => CreateParameters(("enabled", enabled), ("previous_value", previousValue));
}

public sealed class SoundSettingChangedEvent(bool enabled, bool previousValue) : AnalyticsEvent
{
    public override string Name => "sound_setting_changed";
    public override IReadOnlyDictionary<string, object?> Parameters => CreateParameters(("enabled", enabled), ("previous_value", previousValue));
}

public sealed class SaveCompletedEvent(long tick) : AnalyticsEvent
{
    public override string Name => "save_completed";
    public override IReadOnlyDictionary<string, object?> Parameters => CreateParameters(("tick", tick));
}

public sealed class SlowLoadingDetectedEvent(string phase, long durationMilliseconds, string platform) : AnalyticsEvent
{
    public override string Name => "slow_loading_detected";
    public override IReadOnlyDictionary<string, object?> Parameters => CreateParameters(
        ("phase", phase), ("duration_ms", durationMilliseconds), ("platform", platform));
}

public sealed class PerformanceSampleEvent(float fpsAverage, float frameMillisecondsAverage, long memoryMegabytes) : AnalyticsEvent
{
    public override string Name => "performance_sample";
    public override IReadOnlyDictionary<string, object?> Parameters => CreateParameters(
        ("fps_avg", fpsAverage), ("frame_ms_avg", frameMillisecondsAverage), ("memory_mb", memoryMegabytes));
}

public sealed class AppBackgroundedEvent : AnalyticsEvent
{
    public override string Name => "app_backgrounded";
    public override IReadOnlyDictionary<string, object?> Parameters => CreateParameters();
}

public sealed class AppForegroundedEvent : AnalyticsEvent
{
    public override string Name => "app_foregrounded";
    public override IReadOnlyDictionary<string, object?> Parameters => CreateParameters();
}
