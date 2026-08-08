using Vecxy.UI;

namespace HardCore.Cultivation.Game.Presentation;

internal sealed class GameView
{
    public GameView(UiDocument document)
    {
        Document = document;
        WindowLayer = Panel("window-layer");
        WindowBackdrop = Panel("window-backdrop");
        CharacterTapTarget = Button("character-tap-target");
        MissionSummaryButton = Button("mission-summary-button");
        StageName = Text("stage-name");
        YearDial = Radial("year-dial");
        Money = Text("money-text");
        Age = Text("age-text");
        Realm = Text("realm-text");
        CultivationProgressText = Text("cultivation-progress-text");
        CultivationProgress = Progress("cultivation-progress");
        Breakthrough = Button("breakthrough-button");
        ActivityMode = Button("activity-mode-button");
        ActivityModeIcon = Image("activity-mode-icon");
        ActivityModeText = Text("activity-mode-text");

        MissionName = Text("mission-name");
        MissionDescription = Text("mission-description");
        MissionProgressText = Text("mission-progress-text");
        MissionProgress = Progress("mission-progress");
        Effects = Panel("effects-list");

        ShopButton = Button("shop-button");
        InventoryButton = Button("inventory-button");
        ShopWindow = Panel("shop-window");
        ShopMarkup = Text("shop-markup");
        ShopMoney = Text("shop-money");
        ShopGrid = Panel("shop-grid");

        InventoryWindow = Panel("inventory-window");
        InventoryCount = Text("inventory-count");
        SellRate = Text("sell-rate");
        InventoryGrid = Panel("inventory-grid");
        IngredientsTab = Button("ingredients-tab");
        CoresTab = Button("cores-tab");
        PillsTab = Button("pills-tab");
        InventoryDetails = Panel("inventory-details");
        InventoryDetailIconWell = Panel("inventory-detail-icon-well");
        InventoryDetailIcon = Image("inventory-detail-icon");
        InventoryDetailName = Text("inventory-detail-name");
        InventoryDetailEffect = Text("inventory-detail-effect");
        InventoryUse = Button("inventory-use-button");
        InventorySell = Button("inventory-sell-button");

        MissionsWindow = Panel("missions-window");
        AvailableMissionsTab = Button("available-missions-tab");
        AcceptedMissionsTab = Button("accepted-missions-tab");
        AvailableMissionsPage = Panel("available-missions-page");
        AcceptedMissionsPage = Panel("accepted-missions-page");
        MissionQueueCount = Text("mission-queue-count");
        MissionRefresh = Text("mission-refresh");
        MissionQueue = Panel("mission-queue");
        MissionsList = Panel("missions-list");

        BreakthroughWindow = Panel("breakthrough-window");
        BreakthroughChance = Text("breakthrough-chance");
        BreakthroughCost = Text("breakthrough-cost");
        ConfirmBreakthrough = Button("confirm-breakthrough");
        CancelBreakthrough = Button("cancel-breakthrough");
        BreakthroughResult = Panel("breakthrough-result");
        BreakthroughResultTitle = Text("breakthrough-result-title");
        BreakthroughResultText = Text("breakthrough-result-text");
        BreakthroughResultOk = Button("breakthrough-result-ok");

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
        EffectPopupEffect = Text("effect-popup-effect");
        EffectPopupClose = Button("effect-popup-close");
        // Every top-level subtree in the window layer is a mountable surface,
        // including dialogs and popups that intentionally do not use .window.
        Windows = WindowLayer.Children
            .OfType<UiPanel>()
            .Where(panel => panel.Id != "window-backdrop")
            .ToArray();
        WindowCloseButtons = document.QueryAll(".window-close").OfType<UiButton>().ToArray();
    }

    public UiDocument Document { get; }
    public UiPanel WindowLayer { get; }
    public UiPanel WindowBackdrop { get; }
    public UiButton CharacterTapTarget { get; }
    public UiButton MissionSummaryButton { get; }
    public UiText StageName { get; }
    public UiRadialProgress YearDial { get; }
    public UiText Money { get; }
    public UiText Age { get; }
    public UiText Realm { get; }
    public UiText CultivationProgressText { get; }
    public UiProgress CultivationProgress { get; }
    public UiButton Breakthrough { get; }
    public UiButton ActivityMode { get; }
    public UiImage ActivityModeIcon { get; }
    public UiText ActivityModeText { get; }
    public UiText MissionName { get; }
    public UiText MissionDescription { get; }
    public UiText MissionProgressText { get; }
    public UiProgress MissionProgress { get; }
    public UiPanel Effects { get; }
    public UiButton ShopButton { get; }
    public UiButton InventoryButton { get; }
    public UiPanel ShopWindow { get; }
    public UiText ShopMarkup { get; }
    public UiText ShopMoney { get; }
    public UiPanel ShopGrid { get; }
    public UiPanel InventoryWindow { get; }
    public UiText InventoryCount { get; }
    public UiText SellRate { get; }
    public UiPanel InventoryGrid { get; }
    public UiButton IngredientsTab { get; }
    public UiButton CoresTab { get; }
    public UiButton PillsTab { get; }
    public UiPanel InventoryDetails { get; }
    public UiPanel InventoryDetailIconWell { get; }
    public UiImage InventoryDetailIcon { get; }
    public UiText InventoryDetailName { get; }
    public UiText InventoryDetailEffect { get; }
    public UiButton InventoryUse { get; }
    public UiButton InventorySell { get; }
    public UiPanel MissionsWindow { get; }
    public UiButton AvailableMissionsTab { get; }
    public UiButton AcceptedMissionsTab { get; }
    public UiPanel AvailableMissionsPage { get; }
    public UiPanel AcceptedMissionsPage { get; }
    public UiText MissionQueueCount { get; }
    public UiText MissionRefresh { get; }
    public UiPanel MissionQueue { get; }
    public UiPanel MissionsList { get; }
    public UiPanel BreakthroughWindow { get; }
    public UiText BreakthroughChance { get; }
    public UiText BreakthroughCost { get; }
    public UiButton ConfirmBreakthrough { get; }
    public UiButton CancelBreakthrough { get; }
    public UiPanel BreakthroughResult { get; }
    public UiText BreakthroughResultTitle { get; }
    public UiText BreakthroughResultText { get; }
    public UiButton BreakthroughResultOk { get; }
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
    public UiText EffectPopupEffect { get; }
    public UiButton EffectPopupClose { get; }
    public IReadOnlyList<UiPanel> Windows { get; }
    public IReadOnlyList<UiButton> WindowCloseButtons { get; }

    private UiPanel Panel(string id) => Document.GetElementById<UiPanel>(id);
    private UiText Text(string id) => Document.GetElementById<UiText>(id);
    private UiButton Button(string id) => Document.GetElementById<UiButton>(id);
    private UiImage Image(string id) => Document.GetElementById<UiImage>(id);
    private UiProgress Progress(string id) => Document.GetElementById<UiProgress>(id);
    private UiRadialProgress Radial(string id) => Document.GetElementById<UiRadialProgress>(id);
}
