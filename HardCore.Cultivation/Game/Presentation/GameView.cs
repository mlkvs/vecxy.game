using Vecxy.UI;

namespace HardCore.Cultivation.Game.Presentation;

internal sealed class GameWindowDocuments
{
    public GameWindowDocuments(
        UiDocument shop,
        UiDocument inventory,
        UiDocument alchemy,
        UiDocument missions,
        UiDocument breakthrough,
        UiDocument dragonExam,
        UiDocument death,
        UiDocument infoPopup,
        UiDocument effectPopup,
        UiDocument settings,
        UiDocument privacyPolicy)
    {
        Shop = shop;
        Inventory = inventory;
        Alchemy = alchemy;
        Missions = missions;
        Breakthrough = breakthrough;
        DragonExam = dragonExam;
        Death = death;
        InfoPopup = infoPopup;
        EffectPopup = effectPopup;
        Settings = settings;
        PrivacyPolicy = privacyPolicy;
        All = [shop, inventory, alchemy, missions, breakthrough, dragonExam, death, infoPopup, effectPopup, settings, privacyPolicy];
    }

    public UiDocument Shop { get; }
    public UiDocument Inventory { get; }
    public UiDocument Alchemy { get; }
    public UiDocument Missions { get; }
    public UiDocument Breakthrough { get; }
    public UiDocument DragonExam { get; }
    public UiDocument Death { get; }
    public UiDocument InfoPopup { get; }
    public UiDocument EffectPopup { get; }
    public UiDocument Settings { get; }
    public UiDocument PrivacyPolicy { get; }
    public IReadOnlyList<UiDocument> All { get; }
}

internal sealed class GameView
{
    private readonly List<WindowSurface> _surfaces = [];

    public GameView(UiDocument document, GameWindowDocuments windowDocuments)
    {
        Document = document;
        WindowDocuments = windowDocuments;

        CharacterTapTarget = Button("character-tap-target");
        MissionSummaryButton = Button("mission-summary-button");
        YearCandleWax = Progress("year-candle-wax");
        YearCandleCap = Image("year-candle-cap");
        YearCandleFlame = Image("year-candle-flame");
        Money = Text("money-text");
        Age = Text("age-text");
        MaximumAge = Text("maximum-age-text");
        Realm = Text("realm-text");
        CultivationProgressText = Text("cultivation-progress-text");
        CultivationProgress = Progress("cultivation-progress");
        CultivationOverflowProgress = Progress("cultivation-overflow-progress");
        Breakthrough = Button("breakthrough-button");
        ActivityMode = Button("activity-mode-button");
        ActivityModeIcon = Image("activity-mode-icon");
        ActivityModeText = Text("activity-mode-text");

        MissionName = Text("mission-name");
        MissionDescription = Text("mission-description");
        MissionProgressText = Text("mission-progress-text");
        MissionProgress = Progress("mission-progress");
        MissionDangerIndicator = Panel("mission-danger-indicator");
        MissionDifficulty = Text("mission-difficulty");
        MissionCombatMarker = Panel("mission-combat-marker");
        MissionNormalState = Panel("mission-normal-state");
        MissionCombatState = Panel("mission-combat-state");
        MissionCombatPreview = Image("mission-combat-preview");
        CombatHeroDamage = Text("combat-hero-damage");
        CombatEnemyDamage = Text("combat-enemy-damage");
        CombatSurrender = Button("combat-surrender-button");
        HeroHealthProgress = Progress("hero-health-progress");
        HeroRecoveryThreshold = Panel("hero-recovery-threshold");
        HeroHealthText = Text("hero-health-text");
        HeroContaminationProgress = Progress("hero-contamination-progress");
        HeroContaminationText = Text("hero-contamination-text");
        Effects = Panel("effects-list");
        SettingsButton = Button("settings-button");
        DragonExamBadge = Button("dragon-exam-badge");

        ShopButton = Button("shop-button");
        AlchemyButton = Button("alchemy-button");
        InventoryButton = Button("inventory-button");

        ShopWindow = Register(windowDocuments.Shop, "shop-window");
        ShopIngredientsTab = Button(windowDocuments.Shop, "shop-ingredients-tab");
        ShopPillsAndCoresTab = Button(windowDocuments.Shop, "shop-pills-cores-tab");
        ShopGrid = Panel(windowDocuments.Shop, "shop-grid");

        AlchemyWindow = Register(windowDocuments.Alchemy, "alchemy-window");
        AlchemyPillTab = Button(windowDocuments.Alchemy, "alchemy-pill-tab");
        AlchemyDistillTab = Button(windowDocuments.Alchemy, "alchemy-distill-tab");
        AlchemySelection = Panel(windowDocuments.Alchemy, "alchemy-selection");
        AlchemyRarityFilter = Button(windowDocuments.Alchemy, "alchemy-rarity-filter");
        AlchemyQualityFilter = Button(windowDocuments.Alchemy, "alchemy-quality-filter");
        AlchemyTypeFilter = Button(windowDocuments.Alchemy, "alchemy-type-filter");
        AlchemyRarityMenu = Panel(windowDocuments.Alchemy, "alchemy-rarity-menu");
        AlchemyQualityMenu = Panel(windowDocuments.Alchemy, "alchemy-quality-menu");
        AlchemyTypeMenu = Panel(windowDocuments.Alchemy, "alchemy-type-menu");
        AlchemyIngredients = Panel(windowDocuments.Alchemy, "alchemy-ingredients");
        AlchemyCraft = Button(windowDocuments.Alchemy, "alchemy-craft-button");

        InventoryWindow = Register(windowDocuments.Inventory, "inventory-window");
        InventoryCount = Text(windowDocuments.Inventory, "inventory-count");
        InventoryGrid = Panel(windowDocuments.Inventory, "inventory-grid");
        IngredientsTab = Button(windowDocuments.Inventory, "ingredients-tab");
        CoresTab = Button(windowDocuments.Inventory, "cores-tab");
        PillsTab = Button(windowDocuments.Inventory, "pills-tab");
        InventoryDetails = Panel(windowDocuments.Inventory, "inventory-details");
        InventoryDetailIconWell = Panel(windowDocuments.Inventory, "inventory-detail-icon-well");
        InventoryDetailIcon = Image(windowDocuments.Inventory, "inventory-detail-icon");
        InventoryDetailContamination = Text(windowDocuments.Inventory, "inventory-detail-contamination");
        InventoryDetailQuality = Panel(windowDocuments.Inventory, "inventory-detail-quality");
        InventoryDetailName = Text(windowDocuments.Inventory, "inventory-detail-name");
        InventoryDetailRarity = Text(windowDocuments.Inventory, "inventory-detail-rarity");
        InventoryDetailElement = Panel(windowDocuments.Inventory, "inventory-detail-element");
        InventoryDetailElementIcon = Image(windowDocuments.Inventory, "inventory-detail-element-icon");
        InventoryDetailEffect = Text(windowDocuments.Inventory, "inventory-detail-effect");
        InventoryUse = Button(windowDocuments.Inventory, "inventory-use-button");
        InventorySell = Button(windowDocuments.Inventory, "inventory-sell-button");

        MissionsWindow = Register(windowDocuments.Missions, "missions-window");
        AvailableMissionsTab = Button(windowDocuments.Missions, "available-missions-tab");
        AcceptedMissionsTab = Button(windowDocuments.Missions, "accepted-missions-tab");
        AvailableMissionsPage = Panel(windowDocuments.Missions, "available-missions-page");
        AcceptedMissionsPage = Panel(windowDocuments.Missions, "accepted-missions-page");
        MissionQueueCount = Text(windowDocuments.Missions, "mission-queue-count");
        MissionRefresh = Text(windowDocuments.Missions, "mission-refresh");
        MissionQueue = Panel(windowDocuments.Missions, "mission-queue");
        MissionsList = Panel(windowDocuments.Missions, "missions-list");

        BreakthroughWindow = Register(windowDocuments.Breakthrough, "breakthrough-window");
        BreakthroughChance = Text(windowDocuments.Breakthrough, "breakthrough-chance");
        BreakthroughCost = Text(windowDocuments.Breakthrough, "breakthrough-cost");
        ConfirmBreakthrough = Button(windowDocuments.Breakthrough, "confirm-breakthrough");
        CancelBreakthrough = Button(windowDocuments.Breakthrough, "cancel-breakthrough");
        BreakthroughResult = Register(windowDocuments.Breakthrough, "breakthrough-result");
        BreakthroughResultTitle = Text(windowDocuments.Breakthrough, "breakthrough-result-title");
        BreakthroughResultText = Text(windowDocuments.Breakthrough, "breakthrough-result-text");
        BreakthroughResultOk = Button(windowDocuments.Breakthrough, "breakthrough-result-ok");

        DragonExamOverlay = Register(windowDocuments.DragonExam, "dragon-exam-popup");
        DragonExamCurrentRank = Text(windowDocuments.DragonExam, "dragon-exam-current-rank");
        DragonExamNextRank = Text(windowDocuments.DragonExam, "dragon-exam-next-rank");
        DragonExamCopy = Text(windowDocuments.DragonExam, "dragon-exam-copy");
        DragonExamStartLabel = Text(windowDocuments.DragonExam, "dragon-exam-start-label");
        DragonExamStart = Button(windowDocuments.DragonExam, "dragon-exam-start");
        DragonExamLater = Button(windowDocuments.DragonExam, "dragon-exam-later");

        DeathWindow = Register(windowDocuments.Death, "death-window");
        DeathAge = Text(windowDocuments.Death, "death-age");
        DeathStage = Text(windowDocuments.Death, "death-stage");
        DeathYear = Text(windowDocuments.Death, "death-year");
        Restart = Button(windowDocuments.Death, "restart-button");

        InfoPopup = Register(windowDocuments.InfoPopup, "info-popup");
        InfoPopupCard = Panel(windowDocuments.InfoPopup, "info-popup-card");
        InfoPopupIconWell = Panel(windowDocuments.InfoPopup, "info-popup-icon-well");
        InfoPopupIcon = Image(windowDocuments.InfoPopup, "info-popup-icon");
        InfoPopupContamination = Text(windowDocuments.InfoPopup, "info-popup-contamination");
        InfoPopupKind = Text(windowDocuments.InfoPopup, "info-popup-kind");
        InfoPopupTitle = Text(windowDocuments.InfoPopup, "info-popup-title");
        InfoPopupElement = Panel(windowDocuments.InfoPopup, "info-popup-element");
        InfoPopupElementIcon = Image(windowDocuments.InfoPopup, "info-popup-element-icon");
        InfoPopupDescription = Text(windowDocuments.InfoPopup, "info-popup-description");
        InfoPopupEffect = Text(windowDocuments.InfoPopup, "info-popup-effect");
        InfoPopupStatLabel1 = Text(windowDocuments.InfoPopup, "info-popup-stat-label-1");
        InfoPopupPriceIcon = Image(windowDocuments.InfoPopup, "info-popup-price-icon");
        InfoPopupStatValue1 = Text(windowDocuments.InfoPopup, "info-popup-stat-value-1");
        InfoPopupStatLabel2 = Text(windowDocuments.InfoPopup, "info-popup-stat-label-2");
        InfoPopupQuality = Panel(windowDocuments.InfoPopup, "info-popup-quality");
        InfoPopupStatValue2 = Text(windowDocuments.InfoPopup, "info-popup-stat-value-2");
        InfoPopupStatLabel3 = Text(windowDocuments.InfoPopup, "info-popup-stat-label-3");
        InfoPopupStatValue3 = Text(windowDocuments.InfoPopup, "info-popup-stat-value-3");
        InfoPopupDetails = Text(windowDocuments.InfoPopup, "info-popup-details");
        InfoPopupClose = Button(windowDocuments.InfoPopup, "info-popup-close");
        InfoPopupUse = Button(windowDocuments.InfoPopup, "info-popup-use");
        InfoPopupSell = Button(windowDocuments.InfoPopup, "info-popup-sell");
        InfoPopupOk = Button(windowDocuments.InfoPopup, "info-popup-ok");

        EffectPopup = Register(windowDocuments.EffectPopup, "effect-popup");
        EffectPopupCard = Panel(windowDocuments.EffectPopup, "effect-popup-card");
        EffectPopupTitle = Text(windowDocuments.EffectPopup, "effect-popup-title");
        EffectPopupEffect = Text(windowDocuments.EffectPopup, "effect-popup-effect");
        EffectPopupClose = Button(windowDocuments.EffectPopup, "effect-popup-close");

        SettingsWindow = Register(windowDocuments.Settings, "settings-window");
        SettingsMusicToggle = Button(windowDocuments.Settings, "settings-music-toggle");
        SettingsSoundsToggle = Button(windowDocuments.Settings, "settings-sounds-toggle");
        SettingsPrivacyPolicy = Button(windowDocuments.Settings, "settings-privacy-policy");
        SettingsBuildVersion = Text(windowDocuments.Settings, "settings-build-version");

        PrivacyPolicyWindow = Register(windowDocuments.PrivacyPolicy, "privacy-policy-window");
        PrivacyPolicyScroll = Panel(windowDocuments.PrivacyPolicy, "privacy-policy-scroll");
        PrivacyPolicyAccept = Button(windowDocuments.PrivacyPolicy, "privacy-policy-accept");

        Windows = _surfaces.Select(surface => surface.Panel).ToArray();
        WindowLayers = windowDocuments.All.Select(doc => Panel(doc, "window-layer")).ToArray();
        WindowBackdrops = windowDocuments.All
            .Select(doc => doc.Query<UiPanel>("#window-backdrop"))
            .Where(backdrop => backdrop is not null)
            .Cast<UiPanel>()
            .ToArray();
        ModalMoneyTexts = windowDocuments.All
            .Select(doc => doc.Query<UiText>("#modal-money-text"))
            .Where(text => text is not null)
            .Cast<UiText>()
            .ToArray();
        WindowCloseButtons = windowDocuments.All
            .SelectMany(doc => doc.QueryAll(".window-close").OfType<UiButton>())
            .ToArray();
    }

    public UiDocument Document { get; }
    public GameWindowDocuments WindowDocuments { get; }
    public IReadOnlyList<UiPanel> WindowLayers { get; }
    public IReadOnlyList<UiPanel> WindowBackdrops { get; }
    public IReadOnlyList<UiText> ModalMoneyTexts { get; }
    public UiButton CharacterTapTarget { get; }
    public UiButton MissionSummaryButton { get; }
    public UiProgress YearCandleWax { get; }
    public UiImage YearCandleCap { get; }
    public UiImage YearCandleFlame { get; }
    public UiText Money { get; }
    public UiText Age { get; }
    public UiText MaximumAge { get; }
    public UiText Realm { get; }
    public UiText CultivationProgressText { get; }
    public UiProgress CultivationProgress { get; }
    public UiProgress CultivationOverflowProgress { get; }
    public UiButton Breakthrough { get; }
    public UiButton ActivityMode { get; }
    public UiImage ActivityModeIcon { get; }
    public UiText ActivityModeText { get; }
    public UiText MissionName { get; }
    public UiText MissionDescription { get; }
    public UiText MissionProgressText { get; }
    public UiProgress MissionProgress { get; }
    public UiPanel MissionDangerIndicator { get; }
    public UiText MissionDifficulty { get; }
    public UiPanel MissionCombatMarker { get; }
    public UiPanel MissionNormalState { get; }
    public UiPanel MissionCombatState { get; }
    public UiImage MissionCombatPreview { get; }
    public UiText CombatHeroDamage { get; }
    public UiText CombatEnemyDamage { get; }
    public UiButton CombatSurrender { get; }
    public UiProgress HeroHealthProgress { get; }
    public UiPanel HeroRecoveryThreshold { get; }
    public UiText HeroHealthText { get; }
    public UiProgress HeroContaminationProgress { get; }
    public UiText HeroContaminationText { get; }
    public UiPanel Effects { get; }
    public UiButton SettingsButton { get; }
    public UiButton DragonExamBadge { get; }
    public UiPanel DragonExamOverlay { get; }
    public UiText DragonExamCurrentRank { get; }
    public UiText DragonExamNextRank { get; }
    public UiText DragonExamCopy { get; }
    public UiText DragonExamStartLabel { get; }
    public UiButton DragonExamStart { get; }
    public UiButton DragonExamLater { get; }
    public UiButton ShopButton { get; }
    public UiButton AlchemyButton { get; }
    public UiButton InventoryButton { get; }
    public UiPanel ShopWindow { get; }
    public UiButton ShopIngredientsTab { get; }
    public UiButton ShopPillsAndCoresTab { get; }
    public UiPanel ShopGrid { get; }
    public UiPanel AlchemyWindow { get; }
    public UiButton AlchemyPillTab { get; }
    public UiButton AlchemyDistillTab { get; }
    public UiPanel AlchemySelection { get; }
    public UiButton AlchemyRarityFilter { get; }
    public UiButton AlchemyQualityFilter { get; }
    public UiButton AlchemyTypeFilter { get; }
    public UiPanel AlchemyRarityMenu { get; }
    public UiPanel AlchemyQualityMenu { get; }
    public UiPanel AlchemyTypeMenu { get; }
    public UiPanel AlchemyIngredients { get; }
    public UiButton AlchemyCraft { get; }
    public UiPanel InventoryWindow { get; }
    public UiText InventoryCount { get; }
    public UiPanel InventoryGrid { get; }
    public UiButton IngredientsTab { get; }
    public UiButton CoresTab { get; }
    public UiButton PillsTab { get; }
    public UiPanel InventoryDetails { get; }
    public UiPanel InventoryDetailIconWell { get; }
    public UiImage InventoryDetailIcon { get; }
    public UiText InventoryDetailContamination { get; }
    public UiPanel InventoryDetailQuality { get; }
    public UiText InventoryDetailName { get; }
    public UiText InventoryDetailRarity { get; }
    public UiPanel InventoryDetailElement { get; }
    public UiImage InventoryDetailElementIcon { get; }
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
    public UiText InfoPopupContamination { get; }
    public UiText InfoPopupKind { get; }
    public UiText InfoPopupTitle { get; }
    public UiPanel InfoPopupElement { get; }
    public UiImage InfoPopupElementIcon { get; }
    public UiText InfoPopupDescription { get; }
    public UiText InfoPopupEffect { get; }
    public UiText InfoPopupStatLabel1 { get; }
    public UiImage InfoPopupPriceIcon { get; }
    public UiText InfoPopupStatValue1 { get; }
    public UiText InfoPopupStatLabel2 { get; }
    public UiPanel InfoPopupQuality { get; }
    public UiText InfoPopupStatValue2 { get; }
    public UiText InfoPopupStatLabel3 { get; }
    public UiText InfoPopupStatValue3 { get; }
    public UiText InfoPopupDetails { get; }
    public UiButton InfoPopupClose { get; }
    public UiButton InfoPopupUse { get; }
    public UiButton InfoPopupSell { get; }
    public UiButton InfoPopupOk { get; }
    public UiPanel EffectPopup { get; }
    public UiPanel EffectPopupCard { get; }
    public UiText EffectPopupTitle { get; }
    public UiText EffectPopupEffect { get; }
    public UiButton EffectPopupClose { get; }
    public UiPanel SettingsWindow { get; }
    public UiButton SettingsMusicToggle { get; }
    public UiButton SettingsSoundsToggle { get; }
    public UiButton SettingsPrivacyPolicy { get; }
    public UiText SettingsBuildVersion { get; }
    public UiPanel PrivacyPolicyWindow { get; }
    public UiPanel PrivacyPolicyScroll { get; }
    public UiButton PrivacyPolicyAccept { get; }
    public IReadOnlyList<UiPanel> Windows { get; }
    public IReadOnlyList<UiButton> WindowCloseButtons { get; }

    public UiDocument GetWindowDocument(UiPanel panel)
    {
        foreach (var surface in _surfaces)
            if (ReferenceEquals(surface.Panel, panel))
                return surface.Document;
        throw new InvalidOperationException($"Window '{panel.Id}' is not registered.");
    }

    public UiDocument GetDocumentFor(UiElement element)
    {
        for (var current = element; current is not null; current = current.Parent)
        {
            if (ReferenceEquals(current, Document.Root))
                return Document;
            foreach (var document in WindowDocuments.All)
                if (ReferenceEquals(current, document.Root))
                    return document;
        }
        throw new InvalidOperationException($"Element '{element.Id}' does not belong to a known UI document.");
    }

    private UiPanel Register(UiDocument document, string id)
    {
        var panel = Panel(document, id);
        _surfaces.Add(new WindowSurface(document, panel));
        return panel;
    }

    private UiPanel Panel(string id) => Document.GetElementById<UiPanel>(id);
    private UiText Text(string id) => Document.GetElementById<UiText>(id);
    private UiButton Button(string id) => Document.GetElementById<UiButton>(id);
    private UiImage Image(string id) => Document.GetElementById<UiImage>(id);
    private UiProgress Progress(string id) => Document.GetElementById<UiProgress>(id);
    private static UiPanel Panel(UiDocument document, string id) => document.GetElementById<UiPanel>(id);
    private static UiText Text(UiDocument document, string id) => document.GetElementById<UiText>(id);
    private static UiButton Button(UiDocument document, string id) => document.GetElementById<UiButton>(id);
    private static UiImage Image(UiDocument document, string id) => document.GetElementById<UiImage>(id);
    private static UiProgress Progress(UiDocument document, string id) => document.GetElementById<UiProgress>(id);

    private readonly record struct WindowSurface(UiDocument Document, UiPanel Panel);
}
