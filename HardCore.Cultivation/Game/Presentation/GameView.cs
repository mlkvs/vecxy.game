using Vecxy.UI;

namespace HardCore.Cultivation.Game.Presentation;

internal sealed class GameView
{
    public GameView(UiDocument document)
    {
        Document = document;

        WindowLayer = Panel("window-layer");
        CharacterTapTarget = Button("character-tap-target");

        StageName = Text("stage-name");
        Year = Text("year-text");
        Tick = Text("tick-text");
        Money = Text("money-text");
        Spirit = Text("spirit-text");
        Age = Text("age-text");
        Realm = Text("realm-text");
        CultivationCost = Text("cultivation-cost");
        CultivationProgress = Progress("cultivation-progress");
        Advance = Button("advance-button");
        Breakthrough = Button("breakthrough-button");

        MissionName = Text("mission-name");
        MissionDescription = Text("mission-description");
        MissionProgressText = Text("mission-progress-text");
        MissionProgress = Progress("mission-progress");
        Effects = Panel("effects-list");

        ShopButton = Button("shop-button");
        InventoryButton = Button("inventory-button");
        CultivationButton = Button("cultivation-button");
        MissionsButton = Button("missions-button");

        ShopWindow = Panel("shop-window");
        ShopMarkup = Text("shop-markup");
        ShopMoney = Text("shop-money");
        ShopGrid = Panel("shop-grid");

        InventoryWindow = Panel("inventory-window");
        InventoryCount = Text("inventory-count");
        SellRate = Text("sell-rate");
        InventoryGrid = Panel("inventory-grid");

        CultivationWindow = Panel("cultivation-window");
        DetailStage = Text("detail-stage");
        DetailLevel = Text("detail-level");
        DetailCost = Text("detail-cost");
        DetailProgress = Progress("detail-progress");
        DetailAdvance = Button("detail-advance");
        DetailBreakthrough = Button("detail-breakthrough");
        CultivationPath = Panel("cultivation-path-scroll");

        MissionsWindow = Panel("missions-window");
        MissionQueueCount = Text("mission-queue-count");
        MissionRefresh = Text("mission-refresh");
        MissionQueue = Panel("mission-queue");
        MissionsList = Panel("missions-list");

        DeathWindow = Panel("death-window");
        DeathAge = Text("death-age");
        DeathStage = Text("death-stage");
        DeathYear = Text("death-year");
        Restart = Button("restart-button");

        InfoPopup = Panel("info-popup");
        InfoPopupCard = Panel("info-popup-card");
        InfoPopupIconWell = Panel("info-popup-icon-well");
        InfoPopupIcon = Image("info-popup-icon");
        InfoPopupKind = Text("info-popup-kind");
        InfoPopupTitle = Text("info-popup-title");
        InfoPopupDescription = Text("info-popup-description");
        InfoPopupEffect = Text("info-popup-effect");
        InfoPopupStatLabel1 = Text("info-popup-stat-label-1");
        InfoPopupStatValue1 = Text("info-popup-stat-value-1");
        InfoPopupStatLabel2 = Text("info-popup-stat-label-2");
        InfoPopupQuality = Panel("info-popup-quality");
        InfoPopupStatValue2 = Text("info-popup-stat-value-2");
        InfoPopupStatLabel3 = Text("info-popup-stat-label-3");
        InfoPopupStatValue3 = Text("info-popup-stat-value-3");
        InfoPopupDetails = Text("info-popup-details");
        InfoPopupClose = Button("info-popup-close");
        InfoPopupOk = Button("info-popup-ok");

        EffectPopup = Panel("effect-popup");
        EffectPopupCard = Panel("effect-popup-card");
        EffectPopupIconWell = Panel("effect-popup-icon-well");
        EffectPopupIcon = Image("effect-popup-icon");
        EffectPopupKind = Text("effect-popup-kind");
        EffectPopupTitle = Text("effect-popup-title");
        EffectPopupDescription = Text("effect-popup-description");
        EffectPopupEffect = Text("effect-popup-effect");
        EffectPopupStacks = Text("effect-popup-stacks");
        EffectPopupQuality = Panel("effect-popup-quality");
        EffectPopupRarity = Text("effect-popup-rarity");
        EffectPopupDetails = Text("effect-popup-details");
        EffectPopupItem = Button("effect-popup-item");
        EffectPopupClose = Button("effect-popup-close");
        EffectPopupOk = Button("effect-popup-ok");

        Windows = document.QueryAll(".window").OfType<UiPanel>().ToArray();
        WindowCloseButtons = document.QueryAll(".window-close").OfType<UiButton>().ToArray();
    }

    public UiDocument Document { get; }
    public UiPanel WindowLayer { get; }
    public UiButton CharacterTapTarget { get; }
    public UiText StageName { get; }
    public UiText Year { get; }
    public UiText Tick { get; }
    public UiText Money { get; }
    public UiText Spirit { get; }
    public UiText Age { get; }
    public UiText Realm { get; }
    public UiText CultivationCost { get; }
    public UiProgress CultivationProgress { get; }
    public UiButton Advance { get; }
    public UiButton Breakthrough { get; }
    public UiText MissionName { get; }
    public UiText MissionDescription { get; }
    public UiText MissionProgressText { get; }
    public UiProgress MissionProgress { get; }
    public UiPanel Effects { get; }
    public UiButton ShopButton { get; }
    public UiButton InventoryButton { get; }
    public UiButton CultivationButton { get; }
    public UiButton MissionsButton { get; }
    public UiPanel ShopWindow { get; }
    public UiText ShopMarkup { get; }
    public UiText ShopMoney { get; }
    public UiPanel ShopGrid { get; }
    public UiPanel InventoryWindow { get; }
    public UiText InventoryCount { get; }
    public UiText SellRate { get; }
    public UiPanel InventoryGrid { get; }
    public UiPanel CultivationWindow { get; }
    public UiText DetailStage { get; }
    public UiText DetailLevel { get; }
    public UiText DetailCost { get; }
    public UiProgress DetailProgress { get; }
    public UiButton DetailAdvance { get; }
    public UiButton DetailBreakthrough { get; }
    public UiPanel CultivationPath { get; }
    public UiPanel MissionsWindow { get; }
    public UiText MissionQueueCount { get; }
    public UiText MissionRefresh { get; }
    public UiPanel MissionQueue { get; }
    public UiPanel MissionsList { get; }
    public UiPanel DeathWindow { get; }
    public UiText DeathAge { get; }
    public UiText DeathStage { get; }
    public UiText DeathYear { get; }
    public UiButton Restart { get; }
    public UiPanel InfoPopup { get; }
    public UiPanel InfoPopupCard { get; }
    public UiPanel InfoPopupIconWell { get; }
    public UiImage InfoPopupIcon { get; }
    public UiText InfoPopupKind { get; }
    public UiText InfoPopupTitle { get; }
    public UiText InfoPopupDescription { get; }
    public UiText InfoPopupEffect { get; }
    public UiText InfoPopupStatLabel1 { get; }
    public UiText InfoPopupStatValue1 { get; }
    public UiText InfoPopupStatLabel2 { get; }
    public UiPanel InfoPopupQuality { get; }
    public UiText InfoPopupStatValue2 { get; }
    public UiText InfoPopupStatLabel3 { get; }
    public UiText InfoPopupStatValue3 { get; }
    public UiText InfoPopupDetails { get; }
    public UiButton InfoPopupClose { get; }
    public UiButton InfoPopupOk { get; }
    public UiPanel EffectPopup { get; }
    public UiPanel EffectPopupCard { get; }
    public UiPanel EffectPopupIconWell { get; }
    public UiImage EffectPopupIcon { get; }
    public UiText EffectPopupKind { get; }
    public UiText EffectPopupTitle { get; }
    public UiText EffectPopupDescription { get; }
    public UiText EffectPopupEffect { get; }
    public UiText EffectPopupStacks { get; }
    public UiPanel EffectPopupQuality { get; }
    public UiText EffectPopupRarity { get; }
    public UiText EffectPopupDetails { get; }
    public UiButton EffectPopupItem { get; }
    public UiButton EffectPopupClose { get; }
    public UiButton EffectPopupOk { get; }
    public IReadOnlyList<UiPanel> Windows { get; }
    public IReadOnlyList<UiButton> WindowCloseButtons { get; }

    private UiPanel Panel(string id) => Document.GetElementById<UiPanel>(id);
    private UiText Text(string id) => Document.GetElementById<UiText>(id);
    private UiButton Button(string id) => Document.GetElementById<UiButton>(id);
    private UiImage Image(string id) => Document.GetElementById<UiImage>(id);
    private UiProgress Progress(string id) => Document.GetElementById<UiProgress>(id);
}
