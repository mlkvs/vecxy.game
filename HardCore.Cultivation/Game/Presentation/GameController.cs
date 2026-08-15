using System.Globalization;
using System.Numerics;
using System.Collections.Generic;
using System.Reflection;
using HardCore.Cultivation.Game.Application;
using HardCore.Cultivation.Game.Domain;
using HardCore.Cultivation.Game.Infrastructure;
using Vecxy.Audio;
using Vecxy.Kernel;
using Vecxy.Rendering;
using Vecxy.Scene;
using Vecxy.UI;
using GameState = HardCore.Cultivation.Game.Domain.GameState;

namespace HardCore.Cultivation.Game.Presentation;

public sealed class GameController(
    IUiManager ui,
    GameDatabase database,
    GameBuildInfo buildInfo,
    TickProcessor ticks,
    MissionService missions,
    ShopService shop,
    ShopTransactionService transactions,
    ItemEffectService effects,
    ItemPriceCalculator prices,
    CultivationService cultivation,
    AlchemyService alchemy,
    CombatService combat,
    CombatScenePresenter combatScene,
    ISceneManager scenes,
    IRenderer renderer,
    IAudioManager audio,
    GameSaveSystem saves)
{
    private static readonly string[] CultivationPowerColors =
        ["#4daeff", "#56d5a0", "#f1bd59", "#c68bea", "#ef7f59", "#69f3e1"];
    private UiDocument? _document;
    private UiDocument? _floatingDocument;
    private GameWindowDocuments? _windowDocuments;
    private UiDocument? _transientDocument;
    private GameView? _view;
    private UiPanel? _actionToastHost;
    private UiPanel? _actionToast;
    private UiImage? _actionToastIcon;
    private UiText? _actionToastText;
    private Action? _infoPopupAction;
    private Action? _infoPopupUseAction;
    private Action? _infoPopupSellAction;
    private long _actionToastExpiresAt;
    private const long ActionToastLifetimeMilliseconds = 1850;
    private const float HealthUiRefreshIntervalSeconds = 1f / 15f;
    private const string WindowFadeClass = "window-fade-surface";
    private const string WindowOpenClass = "window-fade-open";
    private const string WindowExitToLeftClass = "window-exit-to-left";
    private const float UiReferenceWidth = 620f;
    private const float UiReferenceHeight = 1180f;
    private const int ShopRowCount = 3;
    private const float ShopCardMinimumHeight = 172f;
    private const float ShopGridRowGap = 10f;
    // Header, window/body padding, content margin, and a small safety buffer.
    private const float ShopWindowChromeHeight = 150f;
    private const float ShopViewportMargin = 48f;
    private const int MissionColumnCount = 2;
    private const float MissionCardHeight = 215f;
    private const float MissionCardRowGap = 12f;
    // Window frame (36), header (90), body margin/padding (42), tabs (56),
    // board heading (45), mission-list vertical padding (14), and layout safety (17).
    private const float MissionWindowChromeHeight = 300f;
    private const float MissionViewportMargin = 48f;
    private const float SettingsHeaderHeight = 90f;
    private const float SettingsContentTopPadding = 22f;
    private const float SettingsToggleHeight = 74f;
    private const float SettingsContentGap = 14f;
    private const float SettingsVersionHeight = 20f;
    private const float SettingsWindowVerticalPadding = 36f;
    private const string BackgroundMusicPath = "Musics/Main.mp3";
    private readonly Queue<ActionToastRequest> _actionToastQueue = new();
    private UiPanel? _tapFeedback;
    private UiPanel? _achievementEffect;
    private UiText? _achievementText;
    private GameState _state = null!;
    private float _elapsedMilliseconds;
    private float _yearCandleAnimationSeconds;
    private int _yearCandleFlameFrame = -1;
    private int _yearCandleWaxPixel = int.MinValue;
    private int _yearCandleCapPixel = int.MinValue;
    private string? _yearCandleCapOpacity;
    private bool _deferredHudRefresh;
    private bool _gameOver;
    private bool _applicationPaused;
    private bool _backgroundMusicPaused;
    private bool _privacyPolicyReadToEnd;
    private int _batchedTapCount;
    private decimal _batchedTapPower;
    private float _tapBatchElapsed;
    private int _batchedSpiritualPowerTicks;
    private decimal _batchedSpiritualPower;
    private float _spiritualPowerBatchElapsed;
    private decimal _combatHeroDamage;
    private decimal _combatEnemyDamage;
    private int _combatHeroHits;
    private int _combatEnemyHits;
    private bool _combatWasVictory;
    private string? _combatMissionId;
    private string? _combatEnemyId;
    private ItemCategory _inventoryCategory = ItemCategory.Ingredient;
    private Guid? _selectedInventoryItem;
    private readonly List<Guid?> _alchemySlots = [];
    private readonly List<AlchemySlotWidget> _alchemySlotWidgets = [];
    private readonly List<Guid?> _renderedAlchemySlots = [];
    private Guid? _alchemyCore;
    private AlchemySlotWidget? _alchemyCoreWidget;
    private (AlchemyMode Mode, Guid? Core)? _renderedAlchemyCore;
    private AlchemyMode _alchemyMode;
    private int _alchemyRarityFilter;
    private int _alchemyQualityFilter;
    private int _alchemyTypeFilter;
    private EffectType? _openEffectType;
    private readonly List<FloatingValueWidget> _floatingValues = [];
    private int _floatingValueIndex;
    private readonly List<EffectType> _activeEffectTypes = [];
    private readonly Dictionary<EffectType, (UiRadialProgress Panel, UiRadialProgress Icon)> _effectWidgets = [];
    private int _activeEffectsSignature = int.MinValue;
    private UiKeyedCollection<Guid, ShopSlot, ShopCardView>? _shopCards;
    private UiKeyedCollection<Guid, ItemInstance, InventoryIconView>? _inventoryIcons;
    private UiKeyedCollection<Guid, ItemInstance, InventoryIconView>? _alchemyIngredientIcons;
    private UiKeyedCollection<string, string, MissionCardView>? _missionCards;
    private UiKeyedCollection<Guid, ActiveMission, MissionQueueItemView>? _missionQueueItems;
    private UiText? _missionBoardEmpty;
    private UiText? _missionQueueEmpty;
    private UiText? _shopEmpty;
    private decimal _pendingHealthRestored;
    private float _healthFloatElapsed;
    private float _healthUiElapsed;
    private Character? _characterVisual;
    private Background? _backgroundVisual;

    public GameState State => _state;
    public event Action<TickResult>? TickCompleted;

    public void Initialize()
    {
        var loadedSave = saves.TryLoad(out _state);
        if (!loadedSave)
        {
            InitializeNewGame();
            Track(new FirstLaunchEvent(buildInfo.Platform, buildInfo.Version));
        }
        ApplyMusicSetting();
        if (_state.Shop.Slots.Count == 0)
            shop.Refresh(_state.Shop);
        if (_state.MissionBoard.MissionIds.Count == 0 ||
            _state.MissionBoard.MissionIds.Any(id =>
                Math.Abs(database.GetCultivationStageIndex(database.GetMission(id).StageId) -
                         _state.Character.Cultivation.StageIndex) > 1))
            missions.Refresh(_state);
        combat.ConfigureHero(_state.Character, _state.Character.MaximumHealth <= 0m);
        combatScene.Initialize();
        _gameOver = _state.Character.Age.TotalYears >= cultivation.GetMaximumAge(_state.Character);

        _document = ui.Load("UI/Main.xml");
        _document.Reloaded += BuildUi;
        _floatingDocument = ui.Load("UI/FloatingOverlay.xml");
        _floatingDocument.Reloaded += BuildFloatingUi;
        BuildFloatingUi(_floatingDocument);
        _windowDocuments = LoadWindowDocuments();
        foreach (var windowDocument in _windowDocuments.All)
        {
            windowDocument.Reloaded += HandleWindowDocumentReloaded;
            BuildWindowUi(windowDocument);
            windowDocument.IsVisible = false;
        }
        BuildUi(_document);
        _transientDocument = ui.Load("UI/TransientOverlay.xml");
        _transientDocument.Reloaded += BuildTransientUi;
        BuildTransientUi(_transientDocument);
    }

    public void Update(float deltaTime)
    {
        UpdatePrivacyPolicyReadState();
        _tapBatchElapsed += deltaTime;
        if (_tapBatchElapsed >= 30f)
            FlushTapBatch();
        _spiritualPowerBatchElapsed += deltaTime;
        if (_spiritualPowerBatchElapsed >= 30f)
            FlushSpiritualPowerBatch();
        UpdateYearCandleAnimation(deltaTime);
        if (_actionToast is not null && Environment.TickCount64 >= _actionToastExpiresAt)
        {
            HideActionToast();
            ShowNextActionToast();
        }
        else if (_actionToast is null && _actionToastQueue.Count > 0)
        {
            ShowNextActionToast();
        }

        if (_gameOver)
            return;

        var combatUpdate = combat.Update(_state, deltaTime);
        if (_state.CurrentMission?.Combat is { } activeCombat)
        {
            if (!combatScene.IsVisible)
                combatScene.Show(activeCombat);
            combatScene.Handle(combatUpdate.Events);
            ShowCombatDamage(combatUpdate.Events);
            combatScene.Update(deltaTime);
            UpdateCombatUi();
        }
        else if (combatScene.IsVisible)
        {
            combatScene.Hide();
        }
        if (combatUpdate.StateChanged)
        {
            var combatMission = _state.CurrentMission;
            var combatSnapshot = combatMission?.Combat;
            if (combatUpdate.Events.Any(value => value.Type == CombatEventType.Started))
            {
                _combatWasVictory = false;
                _combatMissionId = combatMission?.MissionConfigId;
                _combatEnemyId = combatSnapshot?.MonsterConfigId;
                Track(new CombatStartedEvent(combatMission?.MissionConfigId, combatSnapshot?.MonsterConfigId,
                    _state.Character.Health, combatSnapshot?.EnemyHealth));
            }
            foreach (var combatEvent in combatUpdate.Events)
            {
                if (combatEvent.Type == CombatEventType.EnemyHurt)
                {
                    _combatHeroDamage += combatEvent.Amount;
                    _combatHeroHits++;
                }
                else if (combatEvent.Type == CombatEventType.HeroHurt)
                {
                    _combatEnemyDamage += combatEvent.Amount;
                    _combatEnemyHits++;
                }
            }
            if (combatUpdate.Events.Any(value => value.Type == CombatEventType.Victory))
            {
                _combatWasVictory = true;
                ShowAchievement("ПОБЕДА");
            }
            if (combatUpdate.Events.Any(value => value.Type == CombatEventType.Defeat))
            {
                ShowAchievement("ПОРАЖЕНИЕ");
            }
            if (combatUpdate.Events.Any(value => value.Type == CombatEventType.Closed))
            {
                Track(new CombatCompletedEvent(_combatWasVictory ? "victory" : "defeat", _combatMissionId,
                    _combatEnemyId, _state.Character.Health));
                if (!_combatWasVictory)
                    Track(new CombatDefeatEvent(_combatMissionId, _combatEnemyId, _state.Character.Cultivation.StageIndex));
                Track(new CombatDamageBatchEvent("player", _combatHeroDamage, _combatHeroHits));
                Track(new CombatDamageBatchEvent("enemy", _combatEnemyDamage, _combatEnemyHits));
                _combatHeroDamage = 0m;
                _combatEnemyDamage = 0m;
                _combatHeroHits = 0;
                _combatEnemyHits = 0;
                _combatMissionId = null;
                _combatEnemyId = null;
                ApplyStateToView();
                SyncMissions();
                Save();
            }
            else
            {
                UpdateMissionSummary();
                UpdateHud();
            }
        }
        if (combatUpdate.HealthChanged)
        {
            _healthUiElapsed += deltaTime;
            if (_healthUiElapsed >= HealthUiRefreshIntervalSeconds ||
                _state.Character.Health >= _state.Character.MaximumHealth)
            {
                UpdateHud();
                _healthUiElapsed = 0f;
            }
            _pendingHealthRestored += combatUpdate.HealthRestored;
            _healthFloatElapsed += deltaTime;
            if (_healthFloatElapsed >= 1f)
            {
                SpawnFloatingValue(_pendingHealthRestored, string.Empty, "health-value");
                _pendingHealthRestored = 0m;
                _healthFloatElapsed = 0f;
            }
        }
        _elapsedMilliseconds += deltaTime * 1000f;
        var processed = 0;
        while (_elapsedMilliseconds >= database.Balance.RealMillisecondsPerTick && processed++ < 100)
        {
            _elapsedMilliseconds -= database.Balance.RealMillisecondsPerTick;
            ProcessWeek();
            if (_gameOver)
                break;
        }
    }

    public void Save()
    {
        FlushTapBatch();
        saves.Save(_state);
        Track(new SaveCompletedEvent(_state.Calendar.TotalTicks));
    }

    public void ChangeMoneyForCheat(long amount)
    {
        if (amount >= 0)
        {
            _state.Character.AddMoney(amount);
        }
        else
        {
            var spend = Math.Min(_state.Character.Money, Math.Abs(amount));
            _state.Character.TrySpendMoney(spend);
        }
        CommitCheatChange();
    }

    public void ChangeSpiritualPowerForCheat(decimal amount)
    {
        if (amount >= 0m)
        {
            _state.Character.AddSpiritualPower(amount);
        }
        else
        {
            var spend = Math.Min(_state.Character.SpiritualPower, Math.Abs(amount));
            if (spend > 0m)
                _state.Character.TrySpendSpiritualPower(spend);
        }
        CommitCheatChange();
    }

    public void ChangeHealthForCheat(decimal amount)
    {
        if (amount >= 0m)
            _state.Character.Heal(amount);
        else
            _state.Character.TakeDamage(Math.Abs(amount));
        CommitCheatChange();
    }

    public void ChangeMaximumHealthForCheat(decimal amount)
    {
        _state.Character.AdjustMaximumHealthOffset(amount);
        combat.ConfigureHero(_state.Character);
        CommitCheatChange();
    }

    public void ChangeAgeForCheat(decimal years)
    {
        _state.Character.Age.Restore(Math.Max(0m, _state.Character.Age.TotalYears + years));
        _gameOver = _state.Character.Age.TotalYears >= cultivation.GetMaximumAge(_state.Character);
        if (!_gameOver && _view is not null)
            CloseWindows();
        CommitCheatChange();
    }

    public void ChangeMaximumAgeForCheat(decimal years)
    {
        _state.Character.AdjustMaximumAgeOffset(years);
        _gameOver = _state.Character.Age.TotalYears >= cultivation.GetMaximumAge(_state.Character);
        CommitCheatChange();
    }

    public void ResetSaveForCheat()
    {
        if (File.Exists(saves.SavePath))
            File.Delete(saves.SavePath);
        InitializeNewGame();
        _elapsedMilliseconds = 0f;
        _healthUiElapsed = 0f;
        _pendingHealthRestored = 0m;
        _gameOver = false;
        _selectedInventoryItem = null;
        CloseWindows();
        CommitCheatChange();
    }

    public void Dispose()
    {
        FlushTapBatch();
        FlushSpiritualPowerBatch();
        if (_document is not null)
        {
            _document.Reloaded -= BuildUi;
            ui.Unload(_document);
        }
        if (_floatingDocument is not null)
        {
            _floatingDocument.Reloaded -= BuildFloatingUi;
            ui.Unload(_floatingDocument);
        }
        if (_windowDocuments is not null)
        {
            foreach (var windowDocument in _windowDocuments.All)
            {
                windowDocument.Reloaded -= HandleWindowDocumentReloaded;
                ui.Unload(windowDocument);
            }
        }
        if (_transientDocument is not null)
        {
            _transientDocument.Reloaded -= BuildTransientUi;
            ui.Unload(_transientDocument);
        }
        _document = null;
        _floatingDocument = null;
        _windowDocuments = null;
        _transientDocument = null;
        _view = null;
        _shopCards = null;
        _inventoryIcons = null;
        _alchemyIngredientIcons = null;
        _alchemySlotWidgets.Clear();
        _renderedAlchemySlots.Clear();
        _alchemyCoreWidget = null;
        _renderedAlchemyCore = null;
        _missionCards = null;
        _missionQueueItems = null;
        _shopEmpty = null;
        _floatingValues.Clear();
        _floatingValueIndex = 0;
        _characterVisual = null;
        _backgroundVisual = null;
        combatScene.Dispose();
    }

    private void InitializeNewGame()
    {
        _state = new GameState(database.Balance.TicksPerYear);
        _state.Character.Restore(0m, 0, database.Balance.StartingAgeYears);
        _state.Character.AddMoney(database.Balance.StartingMoney);
        combat.ConfigureHero(_state.Character, true);
        _state.SetActivityMode(ActivityMode.Cultivation);
        shop.Refresh(_state.Shop);
        missions.Refresh(_state);
    }

    private void CommitCheatChange()
    {
        Save();
        ApplyStateToView();
    }

    private void BuildUi(UiDocument document)
    {
        var windowDocuments = _windowDocuments ?? throw new InvalidOperationException("Window UI documents are not loaded.");
        foreach (var windowDocument in windowDocuments.All)
            BuildWindowUi(windowDocument);
        _view = new GameView(document, windowDocuments);
        _shopCards = new UiKeyedCollection<Guid, ShopSlot, ShopCardView>(
            _view.ShopGrid,
            CreateShopCard,
            card => card.Card,
            UpdateShopCard);
        _inventoryIcons = new UiKeyedCollection<Guid, ItemInstance, InventoryIconView>(
            _view.InventoryGrid,
            CreateInventoryIcon,
            icon => icon.Card,
            UpdateInventoryIcon);
        _alchemyIngredientIcons = new UiKeyedCollection<Guid, ItemInstance, InventoryIconView>(
            _view.AlchemyIngredients,
            CreateAlchemyIngredientIcon,
            icon => icon.Card,
            UpdateAlchemyIngredientIcon);
        _missionCards = new UiKeyedCollection<string, string, MissionCardView>(
            _view.MissionsList,
            CreateMissionCard,
            card => card.Card,
            UpdateMissionCard);
        _missionQueueItems = new UiKeyedCollection<Guid, ActiveMission, MissionQueueItemView>(
            _view.MissionQueue,
            CreateMissionQueueItem,
            item => item.Card,
            UpdateMissionQueueItem);
        InitializeBuiltUi(document);
    }

    private GameWindowDocuments LoadWindowDocuments() =>
        new(
            ui.Load("UI/ShopWindowDocument.xml"),
            ui.Load("UI/InventoryWindowDocument.xml"),
            ui.Load("UI/AlchemyWindowDocument.xml"),
            ui.Load("UI/MissionsWindowDocument.xml"),
            ui.Load("UI/BreakthroughDocument.xml"),
            ui.Load("UI/DeathWindowDocument.xml"),
            ui.Load("UI/InfoPopupDocument.xml"),
            ui.Load("UI/EffectPopupDocument.xml"),
            ui.Load("UI/SettingsWindowDocument.xml"),
            ui.Load("UI/PrivacyPolicyDocument.xml"));

    private void HandleWindowDocumentReloaded(UiDocument document)
    {
        BuildWindowUi(document);
        if (_document is not null)
            BuildUi(_document);
    }

    private void BuildWindowUi(UiDocument document)
    {
        var layer = document.GetElementById<UiPanel>("window-layer");
        foreach (var child in layer.Children.ToArray())
        {
            if (child.Id is not "window-backdrop" and not "modal-money-stat")
                child.RemoveFromParent();
        }
        if (_windowDocuments is null)
            return;

        if (ReferenceEquals(document, _windowDocuments.Shop))
            document.Instantiate("Components/ShopWindow.xml", layer);
        else if (ReferenceEquals(document, _windowDocuments.Inventory))
            document.Instantiate("Components/InventoryWindow.xml", layer);
        else if (ReferenceEquals(document, _windowDocuments.Alchemy))
            document.Instantiate("Components/AlchemyWindow.xml", layer);
        else if (ReferenceEquals(document, _windowDocuments.Missions))
            document.Instantiate("Components/MissionsWindow.xml", layer);
        else if (ReferenceEquals(document, _windowDocuments.Breakthrough))
        {
            document.Instantiate("Components/BreakthroughWindow.xml", layer);
            document.Instantiate("Components/BreakthroughResult.xml", layer);
        }
        else if (ReferenceEquals(document, _windowDocuments.Death))
            document.Instantiate("Components/DeathWindow.xml", layer);
        else if (ReferenceEquals(document, _windowDocuments.InfoPopup))
            document.Instantiate("Components/InfoPopup.xml", layer);
        else if (ReferenceEquals(document, _windowDocuments.EffectPopup))
            document.Instantiate("Components/EffectPopup.xml", layer);
        else if (ReferenceEquals(document, _windowDocuments.Settings))
            document.Instantiate("Components/SettingsWindow.xml", layer);
        else if (ReferenceEquals(document, _windowDocuments.PrivacyPolicy))
            document.Instantiate("Components/PrivacyPolicyWindow.xml", layer);
    }

    private void InitializeBuiltUi(UiDocument document)
    {
        var view = _view!;
        _missionBoardEmpty = null;
        _missionQueueEmpty = null;
        _shopEmpty = null;

        ResetAlchemySlots();
        _alchemyCore = null;
        BuildAlchemySelection();

        BindClick(view.ShopButton, OpenShop);
        BindClick(view.AlchemyButton, OpenAlchemy);
        BindClick(view.InventoryButton, () =>
        {
            TrackScreen("inventory");
            OpenWindow(view.InventoryWindow);
            SyncInventory();
        });
        BindClick(view.SettingsButton, OpenSettings);
        BindClick(view.MissionSummaryButton, OpenMissions);
        BindClick(view.ActivityMode, ToggleActivityMode);
        BindClick(view.Breakthrough, OpenBreakthrough);
        BindClick(view.ConfirmBreakthrough, AttemptBreakthrough);
        BindClick(view.CancelBreakthrough, () => UnmountWindow(view.BreakthroughWindow));
        BindClick(view.BreakthroughResultOk, () => UnmountWindow(view.BreakthroughResult));
        BindClick(view.Restart, RestartGame);
        BindClick(view.InfoPopupOk, ConfirmInfoPopup);
        BindClick(view.InfoPopupUse, UseInfoPopupItem);
        BindClick(view.InfoPopupSell, SellInfoPopupItem);
        BindClick(view.InfoPopupClose, CloseInfoPopup);
        BindClick(view.EffectPopupClose, CloseEffectPopup);
        BindClick(view.SettingsMusicToggle, ToggleMusic);
        BindClick(view.SettingsSoundsToggle, ToggleSounds);
        BindClick(view.SettingsPrivacyPolicy, OpenPrivacyPolicy);
        BindClick(view.PrivacyPolicyAccept, ConfirmPrivacyPolicy);
        view.EffectPopup.Clicked += _ => CloseEffectPopup();
        view.CharacterTapTarget.ClickedAt += (_, position) => TapCharacter(position);
        BindClick(view.AvailableMissionsTab, () => ShowMissionPage(false));
        BindClick(view.AcceptedMissionsTab, () => ShowMissionPage(true));
        BindClick(view.IngredientsTab, () => SelectInventoryCategory(ItemCategory.Ingredient));
        BindClick(view.CoresTab, () => SelectInventoryCategory(ItemCategory.Core));
        BindClick(view.PillsTab, () => SelectInventoryCategory(ItemCategory.Pill));
        BindClick(view.AlchemyPillTab, () => SetAlchemyMode(AlchemyMode.Pill));
        BindClick(view.AlchemyDistillTab, () => SetAlchemyMode(AlchemyMode.Distillation));
        BindClick(view.AlchemyRarityFilter, () => ToggleAlchemyFilterMenu(view.AlchemyRarityMenu));
        BindClick(view.AlchemyQualityFilter, () => ToggleAlchemyFilterMenu(view.AlchemyQualityMenu));
        BindClick(view.AlchemyTypeFilter, () => ToggleAlchemyFilterMenu(view.AlchemyTypeMenu));
        BindClick(view.AlchemyCraft, CraftAlchemy);
        BindClick(view.InventoryUse, UseSelectedItem);
        BindClick(view.InventorySell, SellSelectedItem);
        foreach (var backdrop in view.WindowBackdrops)
            backdrop.Clicked += _ => CloseAlchemyFilterMenus();
        view.AlchemySelection.Clicked += _ => CloseAlchemyFilterMenus();
        view.AlchemyIngredients.Clicked += _ => CloseAlchemyFilterMenus();
        foreach (var close in view.WindowCloseButtons)
            close.Clicked += _ => CloseWindows();

        BuildAlchemyFilterMenus();
        ApplyStateToView();
        PrepareRetainedWindows();
        SyncShop();
        SyncInventory();
        SyncAlchemy();
        SyncMissions();
        if (_gameOver)
            ShowDeathWindow();
        else
            CloseWindows();
        if (!_state.Settings.PrivacyPolicyAccepted)
            OpenPrivacyPolicy();
    }

    private void ProcessWeek()
    {
        var moneyBefore = _state.Character.Money;
        var missionIdBeforeTick = _state.CurrentMission?.MissionConfigId;
        var inventoryBefore = _state.Inventory.Items.Select(item => item.InstanceId).ToHashSet();
        var activeEffectsBefore = _state.ActiveEffects
            .Select(effect => (effect.SourceItemId, effect.Type, effect.Value, effect.RemainingTicks))
            .ToHashSet();
        var result = ticks.ProcessTick(_state);
        foreach (var item in _state.Inventory.Items.Where(item => !inventoryBefore.Contains(item.InstanceId)))
            Track(new ItemReceivedEvent(item.ConfigId, item.Quantity,
                result.MissionCompleted ? "mission_reward" : "tick", item.Contamination));
        foreach (var removed in activeEffectsBefore.Where(effect => !_state.ActiveEffects.Any(current =>
                     (current.SourceItemId, current.Type, current.Value, current.RemainingTicks) == effect)))
            Track(new EffectRemovedEvent(removed.Type.ToString(), "expired", removed.SourceItemId));
        if (result.MissionCompleted)
        {
            Track(new MissionCompletedEvent(missionIdBeforeTick, "completed", _state.Character.Health));
            Track(new MissionRewardReceivedEvent(missionIdBeforeTick, _state.Character.Money - moneyBefore));
            PlaySound("Sounds/mission-complete.wav", 0.65f);
            ShowAchievement("МИССИЯ ВЫПОЛНЕНА");
        }
        if (result.LevelsGained > 0)
        {
            Track(new CultivationLevelGainedEvent("tick", result.LevelsGained,
                _state.Character.Cultivation.StageIndex, _state.Character.Cultivation.Level));
            PlaySound("Sounds/cultivate.wav", 0.6f);
            ShowAchievement(result.LevelsGained == 1 ? "НОВЫЙ УРОВЕНЬ" : $"+{result.LevelsGained} УРОВНЯ");
        }
        if (result.CharacterDied)
        {
            _gameOver = true;
            Track(new CharacterDiedEvent(_state.Character.Age.TotalYears, _state.Character.Cultivation.StageIndex));
            PlaySound("Sounds/death.wav", 0.7f);
        }
        if (result.TickNumber % database.Balance.AutoSaveEveryTicks == 0)
            Save();

        if (HasOpenWindow())
            _deferredHudRefresh = true;
        else
            ApplyStateToView();
        if (result.NewYearStarted)
            SyncShop();
        if (result.MissionCompleted)
            SyncInventory();
        var view = _view!;
        if (IsWindowOpen(view.MissionsWindow))
            SyncMissions();
        if (_openEffectType is not null && IsWindowOpen(view.EffectPopup))
            UpdateEffectPopup();
        if (result.SpiritualPowerGained != 0m)
        {
            _batchedSpiritualPower += result.SpiritualPowerGained;
            _batchedSpiritualPowerTicks++;
            SpawnFloatingValue(result.SpiritualPowerGained, string.Empty, "spirit-value");
        }
        if (result.MissionProgressAdded != 0m)
            SpawnFloatingValue(result.MissionProgressAdded, string.Empty, "mission-value");
        var moneyDelta = _state.Character.Money - moneyBefore;
        if (moneyDelta != 0)
            SpawnFloatingValue(moneyDelta, string.Empty, "money-value");
        if (_gameOver)
        {
            Save();
            ShowDeathWindow();
        }
        TickCompleted?.Invoke(result);
    }

    private void TapCharacter(Vector2 position)
    {
        PlaySound("Sounds/cultivate.wav", 0.35f);
        if (_tapFeedback is not null)
        {
            var feedbackHalfWidth = MathF.Max(0f, _tapFeedback.Bounds.Width * 0.5f);
            var feedbackHalfHeight = MathF.Max(0f, _tapFeedback.Bounds.Height * 0.5f);
            var absoluteX = position.X - feedbackHalfWidth;
            var absoluteY = position.Y - feedbackHalfHeight;
            _tapFeedback.SetStyle("left", $"{absoluteX:0}px");
            _tapFeedback.SetStyle("top", $"{absoluteY:0}px");
            _floatingDocument?.RestartAnimation(_tapFeedback);
        }
        var result = ticks.ProcessTap(_state);
        _batchedTapCount++;
        _batchedTapPower += result.SpiritualPowerGained;
        if (result.SpiritualPowerGained != 0m)
            SpawnFloatingValue(result.SpiritualPowerGained, string.Empty, "spirit-value");
        if (result.LevelsGained > 0)
        {
            Track(new CultivationLevelGainedEvent("tap", result.LevelsGained,
                _state.Character.Cultivation.StageIndex, _state.Character.Cultivation.Level));
            ShowAchievement(result.LevelsGained == 1 ? "НОВЫЙ УРОВЕНЬ" : $"+{result.LevelsGained} УРОВНЯ");
            ApplyStateToView();
        }
        else
        {
            UpdateHud();
        }
    }

    private void SetActivity(ActivityMode mode)
    {
        Track(new UiActionEvent("main", "activity_mode", mode.ToString()));
        _state.SetActivityMode(mode);
        UpdateActivityButtons();
        Save();
    }

    private void ToggleActivityMode() =>
        SetActivity(_state.ActivityMode == ActivityMode.Cultivation
            ? ActivityMode.Missions
            : ActivityMode.Cultivation);

    private void ApplyStateToView()
    {
        if (_view is null)
            return;
        UpdateHud();
        UpdateMissionSummary();
        SyncEffects();
    }

    private void UpdateYearCandleAnimation(float deltaTime)
    {
        if (_view is null)
            return;
        _yearCandleAnimationSeconds += deltaTime;
#if ANDROID
        const float frameDuration = 0.22f;
#else
        const float frameDuration = 0.14f;
#endif
        var frame = (int)(_yearCandleAnimationSeconds / frameDuration) % 6;
        if (frame == _yearCandleFlameFrame)
            return;
        _yearCandleFlameFrame = frame;
        _view.YearCandleFlame.Sprite = $"Assets/Textures/GameUIAtlas.atlas#year-candle-flame-{frame}";
    }

    private void UpdateYearCandleProgress()
    {
        var remaining = 1f - _state.Calendar.TickInYear / (float)_state.Calendar.TicksPerYear;
        remaining = Math.Clamp(remaining, 0f, 1f);
        const float maximumWaxHeight = 94f;
        const float waxBottom = 102f;
        const float fullWaxTop = waxBottom - maximumWaxHeight;
        var waxHeight = Math.Max(2f, maximumWaxHeight * remaining);
        var waxTop = waxBottom - waxHeight;
        var capVisibility = Math.Clamp((remaining - 0.06f) / 0.10f, 0f, 1f);
        var capOverlap = 6f * capVisibility;
        var capTop = Math.Max(fullWaxTop, waxTop - capOverlap);
        var waxPixel = (int)MathF.Round(waxHeight);
        var capPixel = (int)MathF.Round(capTop - fullWaxTop);
        var capOpacity = capVisibility.ToString("0.#", CultureInfo.InvariantCulture);
        if (_yearCandleWaxPixel != waxPixel)
        {
            _yearCandleWaxPixel = waxPixel;
            _view!.YearCandleWax.Progress = waxPixel / maximumWaxHeight;
        }
        if (_yearCandleCapPixel != capPixel)
        {
            _yearCandleCapPixel = capPixel;
            var transform = ToTranslateYString(capPixel);
            _view!.YearCandleCap.Style.Set("transform", transform);
            _view.YearCandleFlame.Style.Set("transform", transform);
        }
        if (_yearCandleCapOpacity != capOpacity)
        {
            _yearCandleCapOpacity = capOpacity;
            _view!.YearCandleCap.Style.Opacity = capOpacity;
        }
    }

    private static string ToTranslateYString(float value) =>
        string.Concat(
            "translateY(",
            MathF.Round(value).ToString(CultureInfo.InvariantCulture),
            "px)");

    private void UpdateHud()
    {
        var character = _state.Character;
        var progress = character.Cultivation;
        var stage = database.Cultivation.Stages[progress.StageIndex];
        var required = cultivation.GetRequiredPower(progress.StageIndex, progress.Level);
        var powerBars = required <= 0m ? 1m : Math.Max(0m, character.SpiritualPower / required);
        UpdateYearCandleProgress();
        _view!.Money.Value = MoneyFormatter.Format(character.Money);
        foreach (var modalMoney in _view.ModalMoneyTexts)
            modalMoney.Value = _view.Money.Value;
        _view.Age.Value = Format(character.Age.TotalYears);
        _view.MaximumAge.Value = Format(cultivation.GetMaximumAge(character));
        _view.Realm.Value = $"{stage.Name} · ур. {progress.Level}";
        UpdateCultivationPowerBar(character.SpiritualPower, required, powerBars);
        var healthFraction = character.MaximumHealth <= 0m ? 0m : character.Health / character.MaximumHealth;
        _view.HeroHealthProgress.Progress = (float)Math.Clamp(healthFraction, 0m, 1m);
        _view.HeroRecoveryThreshold.IsVisible = false;
        _view.HeroHealthText.Value = $"{Format(character.Health)} / {Format(character.MaximumHealth)}";
        _view.HeroContaminationProgress.Progress = (float)Math.Clamp(character.Contamination, 0m, 1m);
        _view.HeroContaminationText.Value = FormatContamination(character.Contamination);
        _view.Breakthrough.IsEnabled = progress.CanAttemptBreakthrough &&
                                      progress.StageIndex < database.Cultivation.Stages.Count - 1 &&
                                      character.SpiritualPower >= required;
        UpdateActivityButtons();
    }

    private void UpdateCultivationPowerBar(decimal spiritualPower, decimal required, decimal powerBars)
    {
        var completedBars = Math.Max(0, (int)decimal.Floor(powerBars));
        if (powerBars < 1m)
        {
            _view!.CultivationProgress.Progress = (float)powerBars;
            _view.CultivationProgress.Style.Set("background-color", CultivationPowerColors[0]);
            _view.CultivationOverflowProgress.Progress = 0f;
            _view.CultivationOverflowProgress.Style.Set("background-color", CultivationPowerColors[1]);
            _view.CultivationProgressText.Value = $"{CompactNumberFormatter.Format(spiritualPower)} / {CompactNumberFormatter.Format(required)}";
            return;
        }

        var remainder = powerBars - completedBars;
        var completedColor = CultivationPowerColors[(completedBars - 1) % CultivationPowerColors.Length];
        var nextColor = CultivationPowerColors[completedBars % CultivationPowerColors.Length];
        _view!.CultivationProgress.Progress = 1f;
        _view.CultivationProgress.Style.Set("background-color", completedColor);
        _view.CultivationOverflowProgress.Progress = (float)Math.Clamp(remainder, 0m, 1m);
        _view.CultivationOverflowProgress.Style.Set("background-color", nextColor);
        var reservePercent = Math.Max(0m, (powerBars - 1m) * 100m);
        _view.CultivationProgressText.Value = reservePercent > 0m
            ? $"{CompactNumberFormatter.Format(spiritualPower)} / {CompactNumberFormatter.Format(required)} · запас +{Format(reservePercent)}%"
            : $"{CompactNumberFormatter.Format(spiritualPower)} / {CompactNumberFormatter.Format(required)}";
    }

    private void UpdateActivityButtons()
    {
        if (_view is null)
            return;
        _view.ActivityMode.SetAttribute("class", _state.ActivityMode == ActivityMode.Missions
            ? "activity-toggle missions"
            : "activity-toggle");
        _view.ActivityModeIcon.Sprite = AtlasSprite(_state.ActivityMode == ActivityMode.Missions
            ? "Assets/Textures/UIIcons/missions.png"
            : "Assets/Textures/UIIcons/cultivation.png");
        _view.ActivityModeText.Value = _state.ActivityMode == ActivityMode.Missions
            ? "МИССИИ"
            : "КУЛЬТИВАЦИЯ";
        SyncActivityScene();
    }

    private void SyncActivityScene()
    {
        var objects = scenes.ActiveScene?.Objects;
        if (objects is null)
            return;

        _characterVisual ??= objects
            .Select(sceneObject => sceneObject.GetComponent<Character>())
            .FirstOrDefault(component => component is not null);
        _backgroundVisual ??= objects
            .Select(sceneObject => sceneObject.GetComponent<Background>())
            .FirstOrDefault(component => component is not null);

        _characterVisual?.PrewarmTextures(renderer);

        var missionMode = _state.ActivityMode == ActivityMode.Missions;
        var stageIndex = Math.Clamp(_state.Character.Cultivation.StageIndex, 0, database.Cultivation.Stages.Count - 1);
        _backgroundVisual?.SetStage(database.Cultivation.Stages[stageIndex]);
        _characterVisual?.SetMissionMode(missionMode);
        _backgroundVisual?.SetMissionMode(missionMode);
    }

    private void UpdateMissionSummary()
    {
        var mission = _state.CurrentMission;
        if (mission is null)
        {
            _view!.MissionName.Value = "Нет активной миссии";
            _view.MissionDescription.Value = "Нажмите, чтобы выбрать поручение.";
            _view.MissionProgressText.Value = "0 / 0";
            _view.MissionProgress.Progress = 0f;
            _view.MissionDangerIndicator.IsVisible = false;
            _view.MissionCombatMarker.IsVisible = false;
            _view.MissionNormalState.IsVisible = true;
            _view.MissionCombatState.IsVisible = false;
            return;
        }
        var config = database.GetMission(mission.MissionConfigId);
        var difficulty = GetMissionDifficulty(config);
        _view!.MissionDangerIndicator.IsVisible = true;
        _view.MissionDifficulty.Value = difficulty.Label;
        _view.MissionDifficulty.ToggleClass("difficulty-very-easy", difficulty.CssClass == "difficulty-very-easy");
        _view.MissionDifficulty.ToggleClass("difficulty-easy", difficulty.CssClass == "difficulty-easy");
        _view.MissionDifficulty.ToggleClass("difficulty-equal", difficulty.CssClass == "difficulty-equal");
        _view.MissionDifficulty.ToggleClass("difficulty-dangerous", difficulty.CssClass == "difficulty-dangerous");
        _view.MissionDifficulty.ToggleClass("difficulty-suicidal", difficulty.CssClass == "difficulty-suicidal");
        var encounter = mission.Encounter;
        var pendingEncounter = encounter is { Resolved: false } && mission.Combat is null;
        _view.MissionCombatMarker.IsVisible = pendingEncounter;
        if (pendingEncounter)
        {
            var markerPosition = mission.RequiredProgress <= 0m
                ? 0m
                : Math.Clamp(encounter!.TriggerProgress / mission.RequiredProgress, 0.02m, 0.98m) * 100m;
            _view.MissionCombatMarker.SetStyle("left",
                markerPosition.ToString("0.##", CultureInfo.InvariantCulture) + "%");
        }
        _view!.MissionName.Value = config.Name;
        _view.MissionDescription.Value = _state.ActivityMode == ActivityMode.Missions
            ? "Выполняется сейчас"
            : "Ожидает: включите режим миссий";
        _view.MissionProgressText.Value = $"{Format(mission.CurrentProgress)} / {Format(mission.RequiredProgress)}";
        _view.MissionProgress.Progress = (float)(mission.RequiredProgress == 0m ? 1m : mission.CurrentProgress / mission.RequiredProgress);
        UpdateCombatUi();
    }

    private void UpdateCombatUi()
    {
        if (_view is null)
            return;
        var active = _state.CurrentMission?.Combat;
        _view.MissionNormalState.IsVisible = active is null;
        _view.MissionCombatState.IsVisible = active is not null;
        if (active is null)
            return;
        if (combatScene.RenderTarget is not null)
            _view.MissionCombatPreview.Texture = combatScene.RenderTarget.ColorTexture;
        var monster = database.GetMonster(active.MonsterConfigId);
        var missionStage = database.GetCultivationStageIndex(database.GetMission(_state.CurrentMission!.MissionConfigId).StageId);
        var enemyStats = combat.GetEnemyStats(missionStage, active.DangerLevel);
        _view.CombatHeroAttackStat.Value = Format(combat.GetHeroAttack(_state.Character, _state.ActiveEffects));
        _view.CombatHeroDefenseStat.Value = Format(combat.GetHeroDefense(_state.Character));
        _view.CombatHeroSpeedStat.Value = Format(combat.GetHeroAttacksPerSecond(_state.Character, _state.ActiveEffects));
        _view.CombatEnemyAttackStat.Value = Format(enemyStats.Attack);
        _view.CombatEnemyDefenseStat.Value = Format(monster.Defense);
        _view.CombatEnemySpeedStat.Value = Format(enemyStats.AttacksPerSecond);
        _view.EnemyHealthProgress.Progress = (float)(active.EnemyMaximumHealth <= 0m
            ? 0m
            : Math.Clamp(active.EnemyHealth / active.EnemyMaximumHealth, 0m, 1m));
        _view.EnemyHealthText.Value = $"{Format(active.EnemyHealth)} / {Format(active.EnemyMaximumHealth)}";
    }

    private void SyncEffects()
    {
        _activeEffectTypes.Clear();
        var signature = new HashCode();
        foreach (var effect in _state.ActiveEffects)
        {
            if (effect.IsExpired)
                continue;
            signature.Add(effect.Type);
            signature.Add(effect.SourceItemId);
            signature.Add(effect.Value);
            signature.Add(effect.DurationType);
            if (!_activeEffectTypes.Contains(effect.Type))
                _activeEffectTypes.Add(effect.Type);
        }
        var contaminationLevel = ContaminationCalculator.GetLevel(_state.Character.Contamination, database.Balance);
        if (contaminationLevel is not null)
        {
            _activeEffectTypes.Add(EffectType.Contamination);
            signature.Add(EffectType.Contamination);
            signature.Add(contaminationLevel.MinimumContamination);
        }
        _activeEffectTypes.Sort((left, right) =>
        {
            var leftIsPermanent = IsPermanentEffectType(left);
            var rightIsPermanent = IsPermanentEffectType(right);
            if (leftIsPermanent != rightIsPermanent)
                return leftIsPermanent ? -1 : 1;
            return left.CompareTo(right);
        });
        var currentSignature = signature.ToHashCode();
        _view!.Effects.IsVisible = _activeEffectTypes.Count > 0;
        if (currentSignature != _activeEffectsSignature)
        {
            _activeEffectsSignature = currentSignature;
            _view.Effects.Clear();
            _effectWidgets.Clear();
            if (_activeEffectTypes.Count == 0)
                return;
            foreach (var type in _activeEffectTypes)
            {
                var effectEntry = (UiRadialProgress)_document!.CreateElement("radial-progress", new Dictionary<string, string>
                {
                    ["class"] = "status-effect-entry",
                    ["sprite"] = "Assets/Textures/GameUIAtlas.atlas#panel-slice",
                    ["clockwise-depletion"] = "true",
                    ["radial-rect"] = "true"
                });
                var effectIcon = (UiRadialProgress)_document.CreateElement("radial-progress", new Dictionary<string, string>
                {
                    ["class"] = "status-effect-icon",
                    ["clockwise-depletion"] = "true"
                });
                if (type == EffectType.Contamination)
                {
                    effectIcon.SetAttribute("sprite", "Assets/Textures/GameUIAtlas.atlas#contamination-effect");
                }
                else if (TryGetFirstActiveEffect(type, out var firstEffect))
                    effectIcon.SetAttribute("sprite", $"Assets/Textures/GameUIAtlas.atlas#effect-{Path.GetFileNameWithoutExtension(database.GetItem(firstEffect.SourceItemId).Icon)}");
                else
                    continue;
                effectEntry.Add(effectIcon);
                effectEntry.Clicked += _ => ShowEffectPopup(type);
                _view.Effects.Add(effectEntry);
                _effectWidgets[type] = (effectEntry, effectIcon);
            }
        }
        foreach (var type in _activeEffectTypes)
            if (_effectWidgets.TryGetValue(type, out var effectWidgets))
            {
                var progress = CalculateEffectTimer(type);
                effectWidgets.Panel.Progress = progress;
                effectWidgets.Icon.Progress = progress;
            }
    }

    private bool TryGetFirstActiveEffect(EffectType type, out ActiveEffect active)
    {
        foreach (var effect in _state.ActiveEffects)
        {
            if (!effect.IsExpired && effect.Type == type)
            {
                active = effect;
                return true;
            }
        }
        active = null!;
        return false;
    }

    private bool IsPermanentEffectType(EffectType type) =>
        type == EffectType.Contamination ||
        _state.ActiveEffects.Any(effect =>
            effect.Type == type &&
            !effect.IsExpired &&
            effect.IsPermanent);

    private void SyncShop()
    {
        if (_view is null || _shopCards is null)
            return;
        UpdateShopWindowHeight();
        var availableSlots = _state.Shop.Slots.Where(slot => slot.AvailableQuantity > 0).ToArray();
        _shopCards.Update(availableSlots, slot => slot.SlotId);
        if (availableSlots.Length == 0)
        {
            if (_shopEmpty is null)
            {
                var document = _view.GetDocumentFor(_view.ShopGrid);
                _shopEmpty = document.CreateText(
                    "Все товары распроданы. Новые появятся в начале следующего года.",
                    new Dictionary<string, string> { ["class"] = "shop-empty" });
                _view.ShopGrid.Add(_shopEmpty);
            }
            return;
        }
        if (_shopEmpty is not null)
        {
            _shopEmpty.RemoveFromParent();
            _shopEmpty = null;
        }
    }

    private void OpenShop()
    {
        TrackScreen("shop");
        Track(new ShopOpenedEvent(_state.Character.Money, _state.Character.Cultivation.StageIndex, _state.Shop.Slots.Count));
        UpdateShopWindowHeight();
        OpenWindow(_view!.ShopWindow);
        SyncShop();
    }

    private void UpdateShopWindowHeight()
    {
        if (_view is null)
            return;

        var outputWidth = Math.Max(1, renderer.GameOutputWidth);
        var outputHeight = Math.Max(1, renderer.GameOutputHeight);
        var scale = Math.Min(outputWidth / UiReferenceWidth, outputHeight / UiReferenceHeight);
        scale = Math.Max(0.0001f, scale);
        var canvasHeight = outputHeight / scale;
        var requiredContentHeight = ShopRowCount * ShopCardMinimumHeight +
                                    (ShopRowCount - 1) * ShopGridRowGap;
        var preferredHeight = ShopWindowChromeHeight + requiredContentHeight;
        var height = Math.Min(preferredHeight, Math.Max(420f, canvasHeight - ShopViewportMargin));
        var top = Math.Max(24f, (canvasHeight - height) * 0.5f);

        _view.ShopWindow.Style.Height = $"{height.ToString(CultureInfo.InvariantCulture)}px";
        _view.ShopWindow.Style["top"] = $"{top.ToString(CultureInfo.InvariantCulture)}px";
        _view.ShopWindow.Style["bottom"] = "auto";
    }

    private ShopCardView CreateShopCard(ShopSlot slot)
    {
        var document = _view!.GetDocumentFor(_view.ShopGrid);
        var root = document.Instantiate("Components/ShopCard.xml", _view.ShopGrid, new Dictionary<string, string>
        {
            ["key"] = slot.SlotId.ToString(), ["icon"] = string.Empty, ["name"] = string.Empty,
            ["effect"] = string.Empty, ["price"] = string.Empty
        });
        var card = new ShopCardView(root);
        card.QualityStars = CreateQualityStars(card.QualityHost);
        card.IconWell.Clicked += _ =>
        {
            if (TryGetGuidAttribute(card.Card, "data-slot-id", out var slotId))
                ShowShopItem(slotId);
        };
        card.Buy.Clicked += _ =>
        {
            if (TryGetGuidAttribute(card.Card, "data-slot-id", out var slotId))
                BuyShopItem(slotId);
        };
        return card;
    }

    private void UpdateShopCard(ShopCardView card, ShopSlot slot, int _)
    {
        var config = database.GetItem(slot.Item.ConfigId);
        var rarity = database.GetRarity(slot.Item.Rarity);
        var unitPrice = prices.GetBuyPrice(slot.Item, _state.Shop);
        card.Card.SetAttribute("data-slot-id", slot.SlotId.ToString());
        card.Icon.Sprite = AtlasSprite(config.Icon);
        card.Name.Value = config.Name;
        card.QualityStars.SetQuality(slot.Item.Quality);
        SetContaminationBadge(card.Contamination, slot.Item.Contamination);
        card.Effect.Value = DescribeItemEffect(config, slot.Item);
        card.Buy.Label = MoneyFormatter.Format(unitPrice);
        card.IconWell.Style.BorderColor = rarity.Color;
        card.Buy.IsEnabled = slot.AvailableQuantity > 0 && _state.Character.Money >= unitPrice;
    }

    private void ShowShopItem(Guid slotId)
    {
        var slot = _state.Shop.Slots.FirstOrDefault(candidate => candidate.SlotId == slotId);
        if (slot is null)
            return;
        var config = database.GetItem(slot.Item.ConfigId);
        var unitPrice = prices.GetBuyPrice(slot.Item, _state.Shop);
        ShowItemPopup(config, slot.Item, "1",
            "Цена покупки указана на карточке товара");
    }

    private void BuyShopItem(Guid slotId)
    {
        var slot = _state.Shop.Slots.FirstOrDefault(candidate => candidate.SlotId == slotId);
        if (slot is not null)
            Buy(slotId, database.GetItem(slot.Item.ConfigId));
    }

    private void Buy(Guid slotId, ItemConfig config)
    {
        var moneyBefore = _state.Character.Money;
        var purchasedContamination = _state.Shop.Slots.FirstOrDefault(slot => slot.SlotId == slotId)?.Item.Contamination;
        var result = transactions.Buy(_state, slotId);
        Track(result.Success
            ? new ShopPurchaseSucceededEvent(config.Id, result.TotalPrice, moneyBefore, _state.Character.Money)
            : new ShopPurchaseFailedEvent(config.Id, result.TotalPrice, moneyBefore, result.Message));
        ShowActionFeedback(result.Success ? $"Куплено: {config.Name} · −{MoneyFormatter.Format(result.TotalPrice)}" : result.Message,
            result.Success ? config.Icon : "Assets/Textures/UIIcons/close.png", result.Success);
        if (result.Success)
        {
            Track(new ItemReceivedEvent(config.Id, 1, "shop", purchasedContamination));
            SpawnFloatingValue(-result.TotalPrice, string.Empty, "money-value");
            UpdateHud();
            SyncShop();
            SyncInventory();
        }
    }

    private void SelectInventoryCategory(ItemCategory category)
    {
        _inventoryCategory = category;
        _view?.InventoryGrid.ScrollTo(Vector2.Zero);
        _selectedInventoryItem = null;
        _view!.InventoryDetails.IsVisible = false;
        SyncInventory();
    }

    private void SyncInventory()
    {
        if (_view is null || _inventoryIcons is null)
            return;
        _view.InventoryCount.Value = $"{_state.Inventory.Items.Sum(item => item.Quantity)} предметов";
        _view.IngredientsTab.ToggleClass("active", _inventoryCategory == ItemCategory.Ingredient);
        _view.CoresTab.ToggleClass("active", _inventoryCategory == ItemCategory.Core);
        _view.PillsTab.ToggleClass("active", _inventoryCategory == ItemCategory.Pill);
        var items = _state.Inventory.Items
            .Where(item => database.GetItem(item.ConfigId).Category == _inventoryCategory)
            .ToArray();
        _inventoryIcons.Update(items, item => item.InstanceId);
        UpdateInventorySelection();
        if (_selectedInventoryItem is { } selected && _state.Inventory.Find(selected) is not null)
            SelectInventoryItem(selected);
        else
            _view.InventoryDetails.IsVisible = false;
    }

    private InventoryIconView CreateInventoryIcon(ItemInstance item)
    {
        var document = _view!.GetDocumentFor(_view.InventoryGrid);
        var root = document.Instantiate("Components/InventoryIcon.xml", _view.InventoryGrid, new Dictionary<string, string>
        {
            ["key"] = item.InstanceId.ToString(), ["icon"] = string.Empty, ["quantity"] = string.Empty
        });
        var icon = new InventoryIconView(root);
        icon.QualityStars = CreateQualityStars(icon.QualityHost);
        root.Clicked += _ =>
        {
            if (TryGetGuidAttribute(root, "data-item-id", out var id))
                SelectInventoryItem(id);
        };
        return icon;
    }

    private void UpdateInventoryIcon(InventoryIconView icon, ItemInstance item, int _)
    {
        var config = database.GetItem(item.ConfigId);
        icon.Card.SetAttribute("data-item-id", item.InstanceId.ToString());
        icon.Icon.Sprite = AtlasSprite(config.Icon);
        icon.QualityStars.SetQuality(item.Quality);
        SetContaminationBadge(icon.Contamination, item.Contamination);
        icon.Quantity.Value = $"×{item.Quantity}";
        icon.IconWell.Style.BorderColor = database.GetRarity(item.Rarity).Color;
        icon.Card.ToggleClass("selected", item.InstanceId == _selectedInventoryItem);
    }

    private void SelectInventoryItem(Guid id)
    {
        var item = _state.Inventory.Find(id);
        if (item is null)
            return;
        _selectedInventoryItem = id;
        UpdateInventorySelection();
        var config = database.GetItem(item.ConfigId);
        var rarity = database.GetRarity(item.Rarity);
        _view!.InventoryDetailIcon.Sprite = AtlasSprite(config.Icon);
        _view.InventoryDetailIconWell.Style.BorderColor = rarity.Color;
        BuildQualityStars(_view.InventoryDetailQuality, item.Quality);
        _view.InventoryDetailName.Value = $"{ItemDisplayName(config, item)} · ×{item.Quantity}";
        _view.InventoryDetailRarity.Value = rarity.DisplayName.ToUpperInvariant();
        _view.InventoryDetailRarity.Style.Color = rarity.Color;
        SetItemElement(_view.InventoryDetailElement, _view.InventoryDetailElementIcon, config.Element);
        _view.InventoryDetailEffect.Value = DescribeItemEffect(config, item);
        _view.InventoryDetailEffect.Value += ContaminationDescription(item.Contamination);
        _view.InventoryUse.IsEnabled = config.Effects.Count > 0 || item.CraftedEffects.Count > 0;
        _view.InventorySell.Label = $"ПРОДАТЬ\n+{MoneyFormatter.Format(prices.GetSellPrice(item, _state.Shop))}";
        _view.InventoryDetails.IsVisible = true;
    }

    private void UpdateInventorySelection()
    {
        if (_view is null)
            return;
        foreach (var card in _view.InventoryGrid.Children)
        {
            var selected = TryGetGuidAttribute(card, "data-item-id", out var id) &&
                           id == _selectedInventoryItem;
            card.ToggleClass("selected", selected);
            card.SetAttribute("aria-selected", selected ? "true" : "false");
        }
    }

    private void UseSelectedItem()
    {
        if (_selectedInventoryItem is not { } id)
            return;
        UseInventoryItem(id);
    }

    private void UseInventoryItem(Guid id)
    {
        if (_state.Inventory.Find(id) is not { } item)
            return;
        var config = database.GetItem(item.ConfigId);
        var contaminationBefore = _state.Character.Contamination;
        var contaminationLevelBefore = GetContaminationLevelNumber(contaminationBefore);
        var effectsBefore = _state.ActiveEffects
            .Select(effect => (effect.SourceItemId, effect.Type, effect.Value, effect.RemainingTicks))
            .ToHashSet();
        var before = _state.Character.SpiritualPower;
        var result = effects.Use(_state, id);
        Track(result.Success
            ? new PillConsumedEvent(item.ConfigId, config.Category.ToString(), item.Contamination)
            : new ItemUseFailedEvent(item.ConfigId, config.Category.ToString(), item.Contamination, result.Message));
        if (result.Success)
            TrackContaminationChange(contaminationBefore, contaminationLevelBefore, "pill");
        foreach (var effect in _state.ActiveEffects.Where(effect =>
                     !effectsBefore.Contains((effect.SourceItemId, effect.Type, effect.Value, effect.RemainingTicks))))
            Track(new EffectAddedEvent(effect.Type.ToString(), effect.RemainingTicks, item.ConfigId));
        var levels = result.Success ? cultivation.AdvanceLevelsAutomatically(_state.Character) : 0;
        ShowActionFeedback(result.Message, result.Success ? config.Icon : "Assets/Textures/UIIcons/close.png", result.Success);
        if (_state.Character.SpiritualPower != before)
            SpawnFloatingValue(_state.Character.SpiritualPower - before, string.Empty, "spirit-value");
        if (levels > 0)
            ShowAchievement(levels == 1 ? "НОВЫЙ УРОВЕНЬ" : $"+{levels} УРОВНЯ");
        if (result.Success)
        {
            if (_selectedInventoryItem == id)
                _selectedInventoryItem = _state.Inventory.Find(id) is null ? null : id;
            ApplyStateToView();
            SyncInventory();
            SyncAlchemy();
            Save();
        }
    }

    private void SellSelectedItem()
    {
        if (_selectedInventoryItem is not { } id)
            return;
        SellInventoryItem(id);
    }

    private void SellInventoryItem(Guid id)
    {
        if (_state.Inventory.Find(id) is not { } item)
            return;
        var config = database.GetItem(item.ConfigId);
        var result = transactions.Sell(_state, id);
        Track(result.Success
            ? new ShopSaleSucceededEvent(item.ConfigId, result.TotalPrice)
            : new ShopSaleFailedEvent(item.ConfigId, result.Message));
        ShowActionFeedback(result.Success ? $"Продано: {ItemDisplayName(config, item)} · +{MoneyFormatter.Format(result.TotalPrice)}" : result.Message,
            result.Success ? "Assets/Textures/UIIcons/money.png" : "Assets/Textures/UIIcons/close.png", result.Success);
        if (result.Success)
        {
            SpawnFloatingValue(result.TotalPrice, string.Empty, "money-value");
            if (_selectedInventoryItem == id)
                _selectedInventoryItem = _state.Inventory.Find(id) is null ? null : id;
            UpdateHud();
            SyncInventory();
            SyncAlchemy();
            SyncShop();
            Save();
        }
    }

    private void OpenAlchemy()
    {
        TrackScreen("alchemy");
        Track(new AlchemyOpenedEvent(_state.Inventory.Items.Count(item =>
            database.GetItem(item.ConfigId).Category == ItemCategory.Ingredient)));
        ResetAlchemySlots();
        _alchemyCore = null;
        _alchemyMode = AlchemyMode.Pill;
        _view?.AlchemyIngredients.ScrollTo(Vector2.Zero);
        CloseAlchemyFilterMenus();
        OpenWindow(_view!.AlchemyWindow);
        SyncAlchemy();
    }

    private void SetAlchemyMode(AlchemyMode mode)
    {
        _alchemyMode = mode;
        _view?.AlchemyIngredients.ScrollTo(Vector2.Zero);
        ResetAlchemySlots();
        _alchemyCore = null;
        CloseAlchemyFilterMenus();
        SyncAlchemy();
    }

    private void ResetAlchemySlots()
    {
        _alchemySlots.Clear();
        EnsureAlchemySlots();
    }

    private void EnsureAlchemySlots()
    {
        while (_alchemySlots.Count < database.Alchemy.MaximumIngredients)
            _alchemySlots.Add(null);
        if (_alchemySlots.Count > database.Alchemy.MaximumIngredients)
            _alchemySlots.RemoveRange(database.Alchemy.MaximumIngredients,
                _alchemySlots.Count - database.Alchemy.MaximumIngredients);
    }

    private IReadOnlyList<AlchemySelection> CurrentAlchemySelection()
    {
        var result = _alchemySlots
            .OfType<Guid>()
            .GroupBy(value => value)
            .Select(group => new AlchemySelection(group.Key, group.Count()))
            .ToList();
        if (_alchemyMode == AlchemyMode.Pill && _alchemyCore is { } core)
            result.Add(new AlchemySelection(core, 1));
        return result;
    }

    private void SyncAlchemy()
    {
        if (_view is null || _document is null)
            return;
        EnsureAlchemySlots();
        var selectedCounts = new Dictionary<Guid, int>();
        for (var index = 0; index < _alchemySlots.Count; index++)
        {
            if (_alchemySlots[index] is not { } instanceId)
                continue;
            var item = _state.Inventory.Find(instanceId);
            var alreadySelected = selectedCounts.GetValueOrDefault(instanceId);
            if (item is null || alreadySelected >= item.Quantity)
            {
                _alchemySlots[index] = null;
                continue;
            }
            selectedCounts[instanceId] = alreadySelected + 1;
        }
        if (_alchemyCore is { } coreId && _state.Inventory.Find(coreId) is null)
            _alchemyCore = null;

        _view.AlchemyPillTab.ToggleClass("active", _alchemyMode == AlchemyMode.Pill);
        _view.AlchemyDistillTab.ToggleClass("active", _alchemyMode == AlchemyMode.Distillation);
        SyncAlchemyFilters();

        UpdateAlchemySlots();
        BuildAlchemyIngredients();
        var preview = alchemy.Preview(_state, CurrentAlchemySelection(), _alchemyMode);
        _view.AlchemyCraft.IsEnabled = preview.CanCraft;
        _view.AlchemyCraft.Label = _alchemyMode == AlchemyMode.Pill ? "СОЗДАТЬ ПИЛЮЛЮ" : "РАФИНИРОВАТЬ";
    }

    private void BuildAlchemySelection()
    {
        _view!.AlchemySelection.Clear();
        var document = _view.GetDocumentFor(_view.AlchemySelection);
        _view.AlchemySelection.Add(document.CreateImage(
            "Assets/Textures/UI/alchemy-room.jpg",
            new Dictionary<string, string> { ["class"] = "alchemy-room" }));
        var furnaceStage = document.CreatePanel(new Dictionary<string, string>
        {
            ["class"] = "alchemy-furnace-stage"
        });
        furnaceStage.Add(document.CreateImage(
            "Assets/Textures/UI/alchemy-furnace.png",
            new Dictionary<string, string> { ["class"] = "alchemy-furnace" }));
        _view.AlchemySelection.Add(furnaceStage);
        _alchemySlotWidgets.Clear();
        _renderedAlchemySlots.Clear();
        EnsureAlchemySlots();
        for (var index = 0; index < database.Alchemy.MaximumIngredients; index++)
        {
            var slot = document.CreateButton(attributes: new Dictionary<string, string>
            {
                ["class"] = $"alchemy-slot alchemy-outer-slot slot-{index + 1}"
            });
            var icon = (UiImage)document.CreateElement("image");
            icon.Style.Set("visibility", "hidden");
            slot.Add(icon);
            var elementIcon = document.CreateImage(string.Empty, new Dictionary<string, string>
            {
                ["class"] = "alchemy-slot-element"
            });
            elementIcon.Style.Set("visibility", "hidden");
            slot.Add(elementIcon);
            var qualityHost = document.CreatePanel(new Dictionary<string, string>
            {
                ["class"] = "alchemy-slot-quality item-icon-quality"
            });
            var quality = CreateQualityStars(document, qualityHost);
            quality.SetQuality(0m);
            qualityHost.Style.Set("visibility", "hidden");
            slot.Add(qualityHost);
            var label = document.CreateText((index + 1).ToString(CultureInfo.InvariantCulture),
                new Dictionary<string, string> { ["class"] = "alchemy-slot-index" });
            slot.Add(label);
            var slotIndex = index;
            slot.Clicked += _ => RemoveAlchemyIngredientAt(slotIndex);
            furnaceStage.Add(slot);
            _alchemySlotWidgets.Add(new AlchemySlotWidget(slot, icon, elementIcon, qualityHost, quality, label));
            _renderedAlchemySlots.Add(null);
        }

        var coreSlot = document.CreateButton(attributes: new Dictionary<string, string>
        {
            ["class"] = "alchemy-slot alchemy-core-slot"
        });
        var coreIcon = (UiImage)document.CreateElement("image");
        coreSlot.Add(coreIcon);
        var coreElementIcon = document.CreateImage(string.Empty, new Dictionary<string, string>
        {
            ["class"] = "alchemy-slot-element"
        });
        coreElementIcon.Style.Set("visibility", "hidden");
        coreSlot.Add(coreElementIcon);
        var coreQualityHost = document.CreatePanel(new Dictionary<string, string>
        {
            ["class"] = "alchemy-slot-quality item-icon-quality"
        });
        var coreQuality = CreateQualityStars(document, coreQualityHost);
        coreQuality.SetQuality(0m);
        coreSlot.Add(coreQualityHost);
        var coreLabel = document.CreateText("ЯДРО",
            new Dictionary<string, string> { ["class"] = "alchemy-core-label" });
        coreSlot.Add(coreLabel);
        coreSlot.Clicked += _ =>
        {
            if (_alchemyMode != AlchemyMode.Pill || _alchemyCore is null)
                return;
            _alchemyCore = null;
            SyncAlchemy();
        };
        furnaceStage.Add(coreSlot);
        _alchemyCoreWidget = new AlchemySlotWidget(
            coreSlot, coreIcon, coreElementIcon, coreQualityHost, coreQuality, coreLabel);
        _renderedAlchemyCore = null;
        UpdateAlchemySlots();
    }

    private void UpdateAlchemySlots()
    {
        for (var index = 0; index < _alchemySlotWidgets.Count; index++)
        {
            var instanceId = _alchemySlots[index];
            if (_renderedAlchemySlots[index] == instanceId)
                continue;
            var widget = _alchemySlotWidgets[index];
            var item = instanceId is { } id ? _state.Inventory.Find(id) : null;
            var config = item is null ? null : database.GetItem(item.ConfigId);
            widget.Root.ToggleClass("filled", item is not null);
            SetPaintVisibility(widget.Icon, item is not null);
            SetPaintVisibility(widget.ElementIcon, config?.Element is not null);
            SetPaintVisibility(widget.QualityHost, item is not null);
            SetPaintVisibility(widget.Label, item is null);
            if (item is not null)
            {
                widget.Icon.Sprite = AtlasSprite(config!.Icon);
                if (config.Element is { } element)
                    widget.ElementIcon.Sprite = AtlasSprite(ElementIcon(element));
                widget.Quality.SetQuality(item.Quality);
            }
            _renderedAlchemySlots[index] = instanceId;
        }

        var coreState = (_alchemyMode, _alchemyCore);
        if (_alchemyCoreWidget is not { } coreWidget || _renderedAlchemyCore == coreState)
            return;
        var core = _alchemyMode == AlchemyMode.Pill && _alchemyCore is { } coreId
            ? _state.Inventory.Find(coreId)
            : null;
        var distillation = _alchemyMode == AlchemyMode.Distillation;
        coreWidget.Root.ToggleClass("equipment", distillation);
        coreWidget.Root.ToggleClass("filled", core is not null);
        SetPaintVisibility(coreWidget.Icon, distillation || core is not null);
        SetPaintVisibility(coreWidget.QualityHost, core is not null);
        SetPaintVisibility(coreWidget.Label, !distillation && core is null);
        if (distillation)
        {
            coreWidget.Icon.Sprite = "Assets/Textures/GameUIAtlas.atlas#alchemy";
            SetPaintVisibility(coreWidget.ElementIcon, false);
        }
        else if (core is not null)
        {
            var config = database.GetItem(core.ConfigId);
            coreWidget.Icon.Sprite = AtlasSprite(config.Icon);
            SetPaintVisibility(coreWidget.ElementIcon, config.Element is not null);
            if (config.Element is { } element)
                coreWidget.ElementIcon.Sprite = AtlasSprite(ElementIcon(element));
            coreWidget.Quality.SetQuality(core.Quality);
        }
        else
            SetPaintVisibility(coreWidget.ElementIcon, false);
        _renderedAlchemyCore = coreState;
    }

    private void BuildAlchemyIngredients()
    {
        if (_view is null || _alchemyIngredientIcons is null)
            return;
        var selectedInstanceIds = _alchemySlots
            .OfType<Guid>()
            .Append(_alchemyCore ?? Guid.Empty)
            .Where(id => id != Guid.Empty)
            .ToHashSet();
        var items = _state.Inventory.Items
                     .Where(item =>
                     {
                         if (selectedInstanceIds.Contains(item.InstanceId))
                             return false;
                         var category = database.GetItem(item.ConfigId).Category;
                         return _alchemyMode == AlchemyMode.Pill
                             ? category == ItemCategory.Core || category == ItemCategory.Ingredient && alchemy.GetProperties(item).Count > 0
                             : category == ItemCategory.Ingredient && alchemy.GetProperties(item).Count > 0;
                     })
                     .Where(MatchesAlchemyFilters)
                     .OrderBy(item => database.GetItem(item.ConfigId).Category == ItemCategory.Core ? 0 : 1)
                     .ThenByDescending(item => item.DistillationLevel)
                     .ThenByDescending(item => item.Rarity)
                     .ThenByDescending(item => item.Quality)
                     .ToArray();
        _alchemyIngredientIcons.Update(items, item => item.InstanceId);
    }

    private InventoryIconView CreateAlchemyIngredientIcon(ItemInstance item)
    {
        var document = _view!.GetDocumentFor(_view.AlchemyIngredients);
        var root = document.Instantiate("Components/InventoryIcon.xml", _view.AlchemyIngredients,
            new Dictionary<string, string>
            {
                ["key"] = item.InstanceId.ToString(), ["icon"] = string.Empty,
                ["quantity"] = string.Empty, ["data-item-id"] = item.InstanceId.ToString()
        });
        var icon = new InventoryIconView(root);
        icon.QualityStars = CreateQualityStars(icon.QualityHost);
        root.Clicked += _ =>
        {
            if (TryGetGuidAttribute(root, "data-item-id", out var id))
                ShowAlchemyItem(id);
        };
        return icon;
    }

    private void UpdateAlchemyIngredientIcon(InventoryIconView icon, ItemInstance item, int _)
    {
        var config = database.GetItem(item.ConfigId);
        var selected = config.Category == ItemCategory.Core
            ? _alchemyCore == item.InstanceId
            : _alchemySlots.Contains(item.InstanceId);
        icon.Card.SetAttribute("data-item-id", item.InstanceId.ToString());
        icon.Icon.Sprite = AtlasSprite(config.Icon);
        icon.QualityStars.SetQuality(item.Quality);
        SetContaminationBadge(icon.Contamination, item.Contamination);
        icon.Quantity.Value = $"×{item.Quantity}";
        icon.IconWell.Style.BorderColor = database.GetRarity(item.Rarity).Color;
        icon.Card.ToggleClass("selected", selected);
    }

    private bool MatchesAlchemyFilters(ItemInstance item)
    {
        if (_alchemyRarityFilter > 0 && (int)item.Rarity != _alchemyRarityFilter - 1)
            return false;
        if (_alchemyQualityFilter > 0 &&
            (int)decimal.Ceiling(Math.Clamp(item.Quality, 0.1m, 5m)) != _alchemyQualityFilter)
            return false;
        if (_alchemyTypeFilter == 0)
            return true;
        var category = database.GetItem(item.ConfigId).Category;
        return _alchemyTypeFilter switch
        {
            1 => category == ItemCategory.Ingredient && item.DistillationLevel == 0,
            2 => category == ItemCategory.Core,
            3 => category == ItemCategory.Ingredient && item.DistillationLevel > 0,
            _ => true
        };
    }

    private void BuildAlchemyFilterMenus()
    {
        _view!.AlchemyRarityMenu.Clear();
        _view.AlchemyQualityMenu.Clear();
        _view.AlchemyTypeMenu.Clear();

        AddAlchemyFilterOption(_view.AlchemyRarityMenu, "ВСЕ", 0, value => _alchemyRarityFilter = value);
        foreach (var rarity in Enum.GetValues<ItemRarity>())
            AddAlchemyFilterOption(
                _view.AlchemyRarityMenu,
                database.GetRarity(rarity).DisplayName.ToUpperInvariant(),
                (int)rarity + 1,
                value => _alchemyRarityFilter = value);

        AddAlchemyFilterOption(_view.AlchemyQualityMenu, "ВСЕ", 0, value => _alchemyQualityFilter = value);
        for (var quality = 1; quality <= 5; quality++)
        {
            var value = quality;
            AddAlchemyFilterOption(
                _view.AlchemyQualityMenu,
                $"{quality - 1}–{quality}★",
                value,
                selected => _alchemyQualityFilter = selected);
        }

        var typeLabels = new[] { "ВСЕ", "СЫРЬЁ", "ЯДРА", "ЭКСТРАКТЫ" };
        for (var type = 0; type < typeLabels.Length; type++)
        {
            var value = type;
            AddAlchemyFilterOption(
                _view.AlchemyTypeMenu,
                typeLabels[type],
                value,
                selected => _alchemyTypeFilter = selected);
        }
        CloseAlchemyFilterMenus();
    }

    private void AddAlchemyFilterOption(UiPanel menu, string label, int value, Action<int> select)
    {
        var document = _view!.GetDocumentFor(menu);
        var option = document.CreateButton(label, new Dictionary<string, string>
        {
            ["class"] = "alchemy-filter-option",
            ["data-filter-value"] = value.ToString(CultureInfo.InvariantCulture)
        });
        option.Clicked += _ =>
        {
            select(value);
            _view?.AlchemyIngredients.ScrollTo(Vector2.Zero);
            CloseAlchemyFilterMenus();
            SyncAlchemy();
        };
        menu.Add(option);
    }

    private void ToggleAlchemyFilterMenu(UiPanel menu)
    {
        var show = !menu.IsVisible;
        CloseAlchemyFilterMenus();
        menu.IsVisible = show;
    }

    private void CloseAlchemyFilterMenus()
    {
        if (_view is null)
            return;
        _view.AlchemyRarityMenu.IsVisible = false;
        _view.AlchemyQualityMenu.IsVisible = false;
        _view.AlchemyTypeMenu.IsVisible = false;
    }

    private void SyncAlchemyFilters()
    {
        var rarityLabel = _alchemyRarityFilter == 0
            ? "ВСЕ"
            : database.GetRarity((ItemRarity)(_alchemyRarityFilter - 1)).DisplayName.ToUpperInvariant();
        var qualityLabel = _alchemyQualityFilter == 0
            ? "ВСЕ"
            : $"{_alchemyQualityFilter - 1}–{_alchemyQualityFilter}★";
        var typeLabel = _alchemyTypeFilter switch
        {
            1 => "СЫРЬЁ",
            2 => "ЯДРА",
            3 => "ЭКСТРАКТЫ",
            _ => "ВСЕ"
        };
        _view!.AlchemyRarityFilter.Label = $"Редк.: {rarityLabel}";
        _view.AlchemyQualityFilter.Label = $"Кач.: {qualityLabel}";
        _view.AlchemyTypeFilter.Label = $"Тип: {typeLabel}";
        _view.AlchemyRarityFilter.ToggleClass("active", _alchemyRarityFilter > 0);
        _view.AlchemyQualityFilter.ToggleClass("active", _alchemyQualityFilter > 0);
        _view.AlchemyTypeFilter.ToggleClass("active", _alchemyTypeFilter > 0);
        UpdateAlchemyFilterMenuSelection(_view.AlchemyRarityMenu, _alchemyRarityFilter);
        UpdateAlchemyFilterMenuSelection(_view.AlchemyQualityMenu, _alchemyQualityFilter);
        UpdateAlchemyFilterMenuSelection(_view.AlchemyTypeMenu, _alchemyTypeFilter);
    }

    private static void UpdateAlchemyFilterMenuSelection(UiPanel menu, int selectedValue)
    {
        foreach (var option in menu.Children.OfType<UiButton>())
        {
            var selected = option.Attributes.TryGetValue("data-filter-value", out var raw) &&
                           int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) &&
                           value == selectedValue;
            option.ToggleClass("selected", selected);
        }
    }

    private void ShowAlchemyItem(Guid instanceId)
    {
        CloseAlchemyFilterMenus();
        var item = _state.Inventory.Find(instanceId);
        if (item is null)
            return;
        var config = database.GetItem(item.ConfigId);
        var propertyText = config.Category == ItemCategory.Core
            ? "Ядро занимает центральную точку и влияет на качество и редкость готовой пилюли."
            : $"Свойства: {string.Join(" · ", alchemy.GetProperties(item)
                .Select(value => database.GetAlchemyProperty(value.PropertyId).DisplayName))}";
        ShowItemPopup(
            config,
            item,
            item.Quantity.ToString(CultureInfo.InvariantCulture),
            propertyText,
            () => AddAlchemyIngredient(instanceId),
            config.Category == ItemCategory.Core ? "ПОМЕСТИТЬ В ЦЕНТР" : "ДОБАВИТЬ В СХЕМУ");
    }

    private void AddAlchemyIngredient(Guid instanceId)
    {
        var item = _state.Inventory.Find(instanceId);
        if (item is null)
            return;
        var config = database.GetItem(item.ConfigId);
        if (config.Category == ItemCategory.Core)
        {
            if (_alchemyMode != AlchemyMode.Pill)
                return;
            _alchemyCore = instanceId;
            SyncAlchemy();
            return;
        }
        EnsureAlchemySlots();
        var emptySlot = _alchemySlots.FindIndex(value => value is null);
        if (emptySlot < 0)
        {
            ShowActionFeedback("Все ячейки смеси уже заполнены.", "Assets/Textures/UIIcons/close.png", false, info: true);
            return;
        }
        var selected = _alchemySlots.Count(value => value == instanceId);
        if (selected >= item.Quantity)
            return;
        _alchemySlots[emptySlot] = instanceId;
        SyncAlchemy();
    }

    private void RemoveAlchemyIngredientAt(int slotIndex)
    {
        EnsureAlchemySlots();
        if (slotIndex < 0 || slotIndex >= _alchemySlots.Count)
            return;
        _alchemySlots[slotIndex] = null;
        SyncAlchemy();
    }

    private void CraftAlchemy()
    {
        CloseAlchemyFilterMenus();
        var selection = CurrentAlchemySelection();
        var preview = alchemy.Preview(_state, selection, _alchemyMode);
        Track(new AlchemyCraftAttemptedEvent(selection.Count, _alchemyMode.ToString()));
        var result = alchemy.Craft(_state, selection, _alchemyMode);
        if (!result.Success || result.Output is not { } output)
        {
            Track(new AlchemyCraftFailedEvent(result.Message, _alchemyMode.ToString()));
            if (result.IngredientsDestroyed)
            {
                ResetAlchemySlots();
                _alchemyCore = null;
                Save();
                SyncAlchemy();
                SyncInventory();
                ShowAlchemyFailurePopup();
                return;
            }
            ShowActionFeedback(result.Message, "Assets/Textures/UIIcons/close.png", false);
            return;
        }
        var mode = _alchemyMode;
        ResetAlchemySlots();
        _alchemyCore = null;
        Save();
        SyncAlchemy();
        SyncInventory();
        var config = database.GetItem(output.ConfigId);
        Track(new AlchemyCraftSucceededEvent(output.ConfigId, output.Contamination, mode.ToString(),
            selection.Sum(value => value.Quantity)));
        Track(new ItemReceivedEvent(output.ConfigId, result.ProducedQuantity, "alchemy", output.Contamination));
        if (preview.Output is { } expected && expected.ConfigId != output.ConfigId)
            Track(new AlchemyCraftAlternateResultEvent(output.ConfigId, expected.ConfigId));
        var sellPrice = prices.GetSellPrice(output, _state.Shop);
        var canUse = config.Effects.Count > 0 || output.CraftedEffects.Count > 0;
        ShowItemPopup(
            config,
            output,
            result.ProducedQuantity.ToString(CultureInfo.InvariantCulture),
            mode == AlchemyMode.Pill ? "Создано в алхимической печи." : "Получено после рафинирования.",
            useAction: canUse ? () => UseInventoryItem(output.InstanceId) : null,
            sellAction: () => SellInventoryItem(output.InstanceId),
            sellPrice: sellPrice);
    }

    private void OpenMissions()
    {
        OpenWindow(_view!.MissionsWindow);
        TrackScreen("missions");
        Track(new MissionsOpenedEvent(_state.Character.Cultivation.StageIndex, _state.MissionBoard.MissionIds.Count));
        SyncMissions();
        UpdateMissionsWindowHeight();
    }

    private void ShowMissionPage(bool accepted)
    {
        _view!.AvailableMissionsPage.IsVisible = !accepted;
        _view.AcceptedMissionsPage.IsVisible = accepted;
        _view.AvailableMissionsTab.ToggleClass("active", !accepted);
        _view.AcceptedMissionsTab.ToggleClass("active", accepted);
        SyncMissions();
    }

    private void SyncMissions()
    {
        SyncMissionBoard();
        SyncMissionQueue();
    }

    private void UpdateMissionsWindowHeight()
    {
        if (_view is null)
            return;

        var outputWidth = Math.Max(1, renderer.GameOutputWidth);
        var outputHeight = Math.Max(1, renderer.GameOutputHeight);
        var scale = Math.Max(0.0001f, Math.Min(outputWidth / UiReferenceWidth, outputHeight / UiReferenceHeight));
        var canvasHeight = outputHeight / scale;
        var stageCapacity = database.GetMissionBoardCapacityForStage(_state.Character.Cultivation.StageIndex);
        var rows = Math.Max(1, (int)Math.Ceiling(stageCapacity / (float)MissionColumnCount));
        var requiredCardsHeight = rows * MissionCardHeight + (rows - 1) * MissionCardRowGap;
        var preferredHeight = MissionWindowChromeHeight + requiredCardsHeight;
        var height = Math.Min(preferredHeight, Math.Max(420f, canvasHeight - MissionViewportMargin));
        var top = Math.Max(24f, (canvasHeight - height) * 0.5f);

        _view.MissionsWindow.Style.Height = CssPixels(height);
        _view.MissionsWindow.Style["top"] = CssPixels(top);
        _view.MissionsWindow.Style["bottom"] = "auto";
    }

    private void SyncMissionBoard()
    {
        if (_view is null || _missionCards is null)
            return;
        var refresh = _state.Calendar.TicksPerYear - _state.Calendar.TickInYear;
        _view.MissionRefresh.Value = $"Обновление через {FormatWeeks(refresh)}";
        if (_state.MissionBoard.MissionIds.Count == 0)
        {
            _missionCards.Update(Array.Empty<string>(), id => id);
            if (_missionBoardEmpty is null)
            {
                var document = _view.GetDocumentFor(_view.MissionsList);
                _missionBoardEmpty = document.CreateText(
                    "Все поручения приняты. Новые появятся в начале следующего года.",
                    new Dictionary<string, string> { ["class"] = "mission-board-empty" });
                _view.MissionsList.Add(_missionBoardEmpty);
            }
            return;
        }
        if (_missionBoardEmpty is not null)
        {
            _missionBoardEmpty.RemoveFromParent();
            _missionBoardEmpty = null;
        }
        _missionCards.Update(_state.MissionBoard.MissionIds, id => id);
    }

    private MissionCardView CreateMissionCard(string missionId)
    {
        var document = _view!.GetDocumentFor(_view.MissionsList);
        var root = document.Instantiate("Components/MissionCard.xml", _view.MissionsList, new Dictionary<string, string>
        {
            ["key"] = missionId, ["name"] = string.Empty,
            ["description"] = string.Empty, ["duration"] = string.Empty
        });
        var card = new MissionCardView(root);
        var mission = database.GetMission(missionId);
        BuildMissionRewardPreview(card.RewardIcons, mission);
        card.Start.Clicked += _ => StartMission(missionId);
        return card;
    }

    private void UpdateMissionCard(MissionCardView card, string missionId, int _)
    {
        var mission = database.GetMission(missionId);
        card.Name.Value = mission.Name;
        card.Description.Value = mission.Description;
        var difficulty = GetMissionDifficulty(mission);
        card.Danger.IsVisible = true;
        card.Danger.Value = difficulty.Label;
        card.Danger.ToggleClass("difficulty-very-easy", difficulty.CssClass == "difficulty-very-easy");
        card.Danger.ToggleClass("difficulty-easy", difficulty.CssClass == "difficulty-easy");
        card.Danger.ToggleClass("difficulty-equal", difficulty.CssClass == "difficulty-equal");
        card.Danger.ToggleClass("difficulty-dangerous", difficulty.CssClass == "difficulty-dangerous");
        card.Danger.ToggleClass("difficulty-suicidal", difficulty.CssClass == "difficulty-suicidal");
        card.Duration.Value = $"{mission.MinimumDurationTicks}–{mission.MaximumDurationTicks} недель";
        card.Start.IsEnabled = _state.MissionQueue.Count < database.Balance.MaximumMissionQueueSize;
    }

    private (string Label, string CssClass) GetMissionDifficulty(MissionConfig mission)
    {
        var delta = database.GetCultivationStageIndex(mission.StageId) - _state.Character.Cultivation.StageIndex;
        if (delta < 0)
            return mission.DangerLevel.GetValueOrDefault() >= 2
                ? ("ЛЕГКО", "difficulty-easy")
                : ("ОЧЕНЬ ЛЕГКО", "difficulty-very-easy");
        if (delta == 0)
            return ("НАРАВНЕ", "difficulty-equal");
        return mission.DangerLevel.GetValueOrDefault() >= 3
            ? ("САМОУБИЙСТВО", "difficulty-suicidal")
            : ("ОПАСНО", "difficulty-dangerous");
    }

    private void StartMission(string missionId)
    {
        var result = missions.Start(_state, missionId);
        Track(result.Success ? new MissionStartedEvent(missionId) : new MissionStartFailedEvent(missionId, result.Message));
        ShowActionFeedback(result.Message,
            result.Success ? "Assets/Textures/UIIcons/missions.png" : "Assets/Textures/UIIcons/close.png",
            result.Success);
        if (result.Success)
            SyncMissions();
        UpdateMissionSummary();
        Save();
    }

    private void SyncMissionQueue()
    {
        if (_view is null || _missionQueueItems is null)
            return;
        _view.MissionQueueCount.Value = $"{_state.MissionQueue.Count} / {database.Balance.MaximumMissionQueueSize}";
        if (_state.MissionQueue.Count == 0)
        {
            _missionQueueItems.Update(Array.Empty<ActiveMission>(), mission => mission.InstanceId);
            if (_missionQueueEmpty is null)
            {
                var document = _view.GetDocumentFor(_view.MissionQueue);
                _missionQueueEmpty = document.CreateText(
                    "Принятых миссий пока нет.",
                    new Dictionary<string, string> { ["class"] = "queue-empty" });
                _view.MissionQueue.Add(_missionQueueEmpty);
            }
            return;
        }
        if (_missionQueueEmpty is not null)
        {
            _missionQueueEmpty.RemoveFromParent();
            _missionQueueEmpty = null;
        }
        _missionQueueItems.Update(_state.MissionQueue, mission => mission.InstanceId);
    }

    private MissionQueueItemView CreateMissionQueueItem(ActiveMission mission)
    {
        var document = _view!.GetDocumentFor(_view.MissionQueue);
        var root = document.Instantiate("Components/MissionQueueItem.xml", _view.MissionQueue, new Dictionary<string, string>
        {
            ["key"] = mission.InstanceId.ToString(), ["number"] = string.Empty,
            ["name"] = string.Empty, ["progress"] = string.Empty
        });
        var card = new MissionQueueItemView(root);
        var id = mission.InstanceId;
        card.MoveUp.Clicked += _ => MoveMission(id, -1);
        card.MoveDown.Clicked += _ => MoveMission(id, 1);
        card.Remove.Clicked += _ => RemoveMission(id);
        return card;
    }

    private void UpdateMissionQueueItem(MissionQueueItemView card, ActiveMission mission, int index)
    {
        var config = database.GetMission(mission.MissionConfigId);
        card.Number.Value = (index + 1).ToString(CultureInfo.InvariantCulture);
        card.Name.Value = config.Name;
        card.Progress.Value = index == 0
            ? $"{Format(mission.CurrentProgress)} / {Format(mission.RequiredProgress)}"
            : $"{Format(mission.RequiredProgress)} недель";
        var queueLocked = _state.CurrentMission?.IsInCombat == true;
        card.MoveUp.IsEnabled = !queueLocked && index > 0;
        card.MoveDown.IsEnabled = !queueLocked && index < _state.MissionQueue.Count - 1;
        card.Remove.IsEnabled = !mission.IsInCombat;
    }

    private void MoveMission(Guid id, int offset)
    {
        var result = missions.Move(_state, id, offset);
        ShowActionFeedback(result.Message, "Assets/Textures/UIIcons/missions.png", result.Success, result.Success);
        SyncMissionQueue();
        UpdateMissionSummary();
    }

    private void RemoveMission(Guid id)
    {
        var result = missions.Remove(_state, id);
        ShowActionFeedback(result.Message, "Assets/Textures/UIIcons/missions.png", result.Success, result.Success);
        SyncMissions();
        UpdateMissionSummary();
    }

    private void OpenBreakthrough()
    {
        var progress = _state.Character.Cultivation;
        var required = cultivation.GetRequiredPower(progress.StageIndex, progress.Level);
        _view!.BreakthroughChance.Value = $"{Format(cultivation.GetBreakthroughChance(_state.Character, _state.ActiveEffects))}%";
        _view.BreakthroughCost.Value =
            $"Нужно: {CompactNumberFormatter.Format(required)} · накоплено: {CompactNumberFormatter.Format(_state.Character.SpiritualPower)}\nПосле успеха запас обнулится";
        OpenWindow(_view.BreakthroughWindow);
        TrackScreen("breakthrough");
    }

    private void AttemptBreakthrough()
    {
        UnmountWindow(_view!.BreakthroughWindow);
        var beforeStage = _state.Character.Cultivation.StageIndex;
        var result = cultivation.AttemptBreakthrough(_state.Character, _state.ActiveEffects);
        Track(new BreakthroughAttemptedEvent(beforeStage, result.FinalChance));
        Track(result.Success
            ? new BreakthroughSucceededEvent(beforeStage, _state.Character.Cultivation.StageIndex, result.FinalChance, result.LevelsLost)
            : new BreakthroughFailedEvent(beforeStage, _state.Character.Cultivation.StageIndex, result.FinalChance, result.LevelsLost));
        combat.ConfigureHero(_state.Character);
        _view.BreakthroughResultTitle.Value = result.Success ? "ПРОРЫВ УСПЕШЕН" : "ПРОРЫВ НЕ УДАЛСЯ";
        _view.BreakthroughResultText.Value = result.Success
            ? "Вы перешли на новую ступень культивации."
            : $"Прорыв не удался, вы получили травму и потеряли {result.LevelsLost} уровней";
        OpenWindow(_view.BreakthroughResult);
        if (result.Success)
        {
            PlaySound("Sounds/breakthrough.wav", 0.7f);
            missions.Refresh(_state);
            SyncMissionBoard();
            ShowAchievement("УСПЕШНЫЙ ПРОРЫВ");
            ShowActionFeedback($"Предел жизни увеличен до {Format(cultivation.GetMaximumAge(_state.Character))} лет.",
                "Assets/Textures/UIIcons/age.png", true, info: true);
        }
        ApplyStateToView();
        Save();
    }

    private void ShowEffectPopup(EffectType type)
    {
        _openEffectType = type;
        UpdateEffectPopup();
        MountWindow(_view!.EffectPopup, exclusive: false);
    }

    private void UpdateEffectPopup()
    {
        if (_openEffectType is not { } type)
            return;
        _view!.EffectPopupTitle.Value = type == EffectType.Contamination
            ? $"Загрязнение Ур. {GetContaminationLevelNumber()}"
            : EffectName(type);
        if (type == EffectType.Contamination)
        {
            var level = ContaminationCalculator.GetLevel(_state.Character.Contamination, database.Balance);
            _view.EffectPopupEffect.Value = level is null
                ? "Загрязнение отсутствует."
                : $"Загрязнение Ур. {GetContaminationLevelNumber()} · {level.Name}\n" +
                  string.Join("; ", level.Effects.Select(effect => DescribeEffect(effect, 1m, false)));
            return;
        }
        var hasActive = false;
        var hasUntilBreakthrough = false;
        var hasTemporary = false;
        var minimumRemainingTicks = int.MaxValue;
        var description = string.Empty;
        foreach (var effect in _state.ActiveEffects)
        {
            if (effect.Type != type)
                continue;
            hasActive = true;
            hasUntilBreakthrough |= effect.IsUntilBreakthroughAttempt;
            hasTemporary |= !effect.IsPermanent;
            if (!effect.IsPermanent)
                minimumRemainingTicks = Math.Min(minimumRemainingTicks, Math.Max(0, effect.RemainingTicks ?? 0));
            var itemDescription = DescribeEffect(
                new ItemEffectDefinition { Type = effect.Type, Operation = effect.Operation, Value = effect.Value },
                1m,
                effect.DurationType == ItemDurationType.Temporary);
            description = description.Length == 0 ? itemDescription : $"{description}; {itemDescription}";
        }
        if (!hasActive)
        {
            _view!.EffectPopupEffect.Value = $"{EffectName(type)}: осталось 0 недель";
            return;
        }
        var duration = hasUntilBreakthrough
            ? "к следующей попытке прорыва"
            : !hasTemporary
                ? string.Empty
                : $"на {FormatDuration(minimumRemainingTicks)}";
        _view!.EffectPopupEffect.Value = $"{description}{(string.IsNullOrEmpty(duration) ? string.Empty : $" · {duration}")}";
    }

    private void CloseEffectPopup()
    {
        if (_view is not null)
            UnmountWindow(_view.EffectPopup);
        _openEffectType = null;
    }

    private float CalculateEffectTimer(EffectType type)
    {
        if (type == EffectType.Contamination)
            return 1f;

        var hasTemporary = false;
        var remainingFraction = 1f;
        foreach (var effect in _state.ActiveEffects)
        {
            if (effect.Type != type || effect.IsExpired)
                continue;
            if (effect.IsUntilBreakthroughAttempt || effect.IsPermanent)
                return 1f;

            hasTemporary = true;
            var duration = Math.Max(1, database.GetItem(effect.SourceItemId).TemporaryDurationTicks);
            remainingFraction = Math.Min(
                remainingFraction,
                (float)Math.Clamp((effect.RemainingTicks ?? 0) / (decimal)duration, 0m, 1m));
        }

        return hasTemporary ? remainingFraction : 1f;
    }

    private void OpenWindow(UiPanel window)
    {
        MountWindow(window, exclusive: true);
    }

    private void TrackScreen(string screen) =>
        Track(new ScreenViewEvent(screen));

    private static void Track(AnalyticsEvent analyticsEvent) => analyticsEvent.Publish();

    private void TrackContaminationChange(decimal before, int beforeLevel, string source)
    {
        var after = _state.Character.Contamination;
        if (before == after)
            return;
        var afterLevel = GetContaminationLevelNumber(after);
        Track(new ContaminationChangedEvent(before, after, source, afterLevel));
        if (beforeLevel != afterLevel)
            Track(new ContaminationLevelChangedEvent(beforeLevel, afterLevel, after));
        if (after < before)
            Track(new PurificationAppliedEvent(before, after, before - after));
    }

    private void OpenSettings()
    {
        TrackScreen("settings");
        Track(new SettingsOpenedEvent(buildInfo.Version, buildInfo.VersionCode));
        SyncSettings();
        UpdateSettingsWindowHeight();
        OpenWindow(_view!.SettingsWindow);
    }

    private void UpdateSettingsWindowHeight()
    {
        if (_view is null)
            return;

        var outputWidth = Math.Max(1, renderer.GameOutputWidth);
        var outputHeight = Math.Max(1, renderer.GameOutputHeight);
        var scale = Math.Max(0.0001f, Math.Min(outputWidth / UiReferenceWidth, outputHeight / UiReferenceHeight));
        var canvasHeight = outputHeight / scale;
        var contentHeight = SettingsContentTopPadding + SettingsToggleHeight * 3f +
                            SettingsContentGap * 3f + SettingsVersionHeight;
        var height = SettingsHeaderHeight + SettingsWindowVerticalPadding + contentHeight;
        var top = Math.Max(24f, (canvasHeight - height) * 0.5f);

        _view.SettingsWindow.Style.Height = CssPixels(height);
        _view.SettingsWindow.Style["top"] = CssPixels(top);
        _view.SettingsWindow.Style["bottom"] = "auto";
    }

    private void ToggleMusic()
    {
        var previous = _state.Settings.MusicEnabled;
        _state.Settings.ToggleMusic();
        Track(new MusicSettingChangedEvent(_state.Settings.MusicEnabled, previous));
        ApplyMusicSetting();
        Save();
        SyncSettings();
        PlaySound("Sounds/ui-click.wav", 0.45f);
    }

    private void ToggleSounds()
    {
        var previous = _state.Settings.SoundsEnabled;
        _state.Settings.ToggleSounds();
        Track(new SoundSettingChangedEvent(_state.Settings.SoundsEnabled, previous));
        Save();
        SyncSettings();
        PlaySound("Sounds/ui-click.wav", 0.45f);
    }

    private void SyncSettings()
    {
        if (_view is null)
            return;
        SetSettingsToggle(_view.SettingsMusicToggle, "МУЗЫКА", _state.Settings.MusicEnabled);
        SetSettingsToggle(_view.SettingsSoundsToggle, "ЗВУКИ", _state.Settings.SoundsEnabled);
        _view.SettingsBuildVersion.Value = $"Версия {buildInfo.DisplayVersion}";
    }

    private void OpenPrivacyPolicy()
    {
        if (_view is null)
            return;

        _view.PrivacyPolicyScroll.ScrollTo(Vector2.Zero);
        _privacyPolicyReadToEnd = _state.Settings.PrivacyPolicyAccepted;
        SyncPrivacyPolicyControls();
        MountWindow(_view.PrivacyPolicyWindow, exclusive: true);
    }

    private void UpdatePrivacyPolicyReadState()
    {
        if (_view is null || _state.Settings.PrivacyPolicyAccepted ||
            !IsWindowOpen(_view.PrivacyPolicyWindow) || _privacyPolicyReadToEnd)
        {
            return;
        }

        var scroll = _view.PrivacyPolicyScroll;
        if (scroll.Bounds.Height <= 1f || scroll.ScrollExtent.Y <= 1f)
            return;
        var maximumOffset = Math.Max(0f, scroll.ScrollExtent.Y - scroll.Bounds.Height);
        if (maximumOffset > 1f && scroll.ScrollOffset.Y < maximumOffset - 1f)
            return;

        _privacyPolicyReadToEnd = true;
        SyncPrivacyPolicyControls();
    }

    private void SyncPrivacyPolicyControls()
    {
        if (_view is null)
            return;

        var accepted = _state.Settings.PrivacyPolicyAccepted;
        var ready = accepted || _privacyPolicyReadToEnd;
        _view.PrivacyPolicyAccept.Label = accepted ? "ЗАКРЫТЬ" : "ПРИНЯТЬ";
        _view.PrivacyPolicyAccept.IsEnabled = ready;
        _view.PrivacyPolicyAccept.ToggleClass("is-disabled", !ready);
    }

    private void ConfirmPrivacyPolicy()
    {
        if (!_state.Settings.PrivacyPolicyAccepted && !_privacyPolicyReadToEnd)
            return;
        if (!_state.Settings.PrivacyPolicyAccepted)
        {
            _state.Settings.AcceptPrivacyPolicy();
            Save();
        }

        UnmountWindow(_view!.PrivacyPolicyWindow);
        PlaySound("Sounds/ui-click.wav", 0.45f);
    }

    private void ApplyMusicSetting()
    {
        try
        {
            if (_state.Settings.MusicEnabled && !_applicationPaused)
            {
                if (_backgroundMusicPaused)
                    audio.Resume(BackgroundMusicPath, loop: true);
                else
                    audio.Play(BackgroundMusicPath, loop: true, volume: 0.35f);
                _backgroundMusicPaused = false;
            }
            else
            {
                audio.Stop(BackgroundMusicPath, loop: true);
                _backgroundMusicPaused = false;
            }
        }
        catch { }
    }

    public void SetApplicationActive(bool isActive)
    {
        _applicationPaused = !isActive;
        if (!isActive)
        {
            FlushTapBatch();
            FlushSpiritualPowerBatch();
            Track(new AppBackgroundedEvent());
            _backgroundMusicPaused = _state.Settings.MusicEnabled;
            if (_backgroundMusicPaused)
                audio.Pause(BackgroundMusicPath, loop: true);
            return;
        }

        Track(new AppForegroundedEvent());
        ApplyMusicSetting();
    }

    private void FlushTapBatch()
    {
        if (_batchedTapCount == 0)
            return;
        Track(new TapBatchEvent(_batchedTapCount, _batchedTapPower, _state.Character.Cultivation.StageIndex));
        _batchedTapCount = 0;
        _batchedTapPower = 0m;
        _tapBatchElapsed = 0f;
    }

    private void FlushSpiritualPowerBatch()
    {
        if (_batchedSpiritualPowerTicks == 0)
            return;
        Track(new SpiritualPowerGainedEvent("tick_batch", _batchedSpiritualPower,
            _batchedSpiritualPowerTicks, _state.Character.Cultivation.StageIndex));
        _batchedSpiritualPowerTicks = 0;
        _batchedSpiritualPower = 0m;
        _spiritualPowerBatchElapsed = 0f;
    }

    private static void SetSettingsToggle(UiButton button, string label, bool enabled)
    {
        button.Label = $"{label}: {(enabled ? "ВКЛ" : "ВЫКЛ")}";
        button.ToggleClass("is-disabled", !enabled);
    }

    private void MountWindow(UiPanel window, bool exclusive)
    {
        if (exclusive)
            CloseWindows();
        var document = _view!.GetWindowDocument(window);
        document.IsVisible = true;
        window.IsVisible = true;
        SetPaintVisibility(window, true);
        window.RemoveClass(WindowExitToLeftClass);
        window.AddClass(WindowOpenClass);
        UpdateWindowLayerState();
    }

    private void UnmountWindow(UiPanel window)
    {
        if (window.Classes.Contains(WindowExitToLeftClass))
            return;

        if (!window.Classes.Contains(WindowOpenClass))
        {
            window.RemoveClass(WindowExitToLeftClass);
            window.RemoveClass(WindowOpenClass);
            SetPaintVisibility(window, false);
            UpdateWindowLayerState();
            return;
        }

        window.IsVisible = true;
        window.AddClass(WindowExitToLeftClass);
        window.RemoveClass(WindowOpenClass);
        UpdateWindowLayerState();
    }

    private void PrepareRetainedWindows()
    {
        foreach (var window in _view!.Windows)
        {
            window.AddClass(WindowFadeClass);
            window.RemoveClass(WindowExitToLeftClass);
            window.RemoveClass(WindowOpenClass);
            window.TransitionEnded -= HandleWindowFadeEnded;
            window.TransitionEnded += HandleWindowFadeEnded;
            window.IsVisible = true;
            SetPaintVisibility(window, false);
        }
        foreach (var backdrop in _view.WindowBackdrops)
        {
            backdrop.AddClass(WindowFadeClass);
            backdrop.RemoveClass(WindowOpenClass);
            backdrop.TransitionEnded -= HandleWindowFadeEnded;
            backdrop.TransitionEnded += HandleWindowFadeEnded;
            backdrop.IsVisible = true;
            SetPaintVisibility(backdrop, false);
        }
        foreach (var windowDocument in _view.WindowDocuments.All)
            windowDocument.IsVisible = false;
    }

    private void HandleWindowFadeEnded(UiElement element, UiTransitionEvent transition)
    {
        if (!transition.Property.Equals("opacity", StringComparison.OrdinalIgnoreCase) ||
            element.Classes.Contains(WindowOpenClass))
            return;

        element.RemoveClass(WindowExitToLeftClass);
        SetPaintVisibility(element, false);
        UpdateWindowLayerState();
    }

    private static bool IsWindowOpen(UiElement window) =>
        !string.Equals(window.Style["visibility"], "hidden", StringComparison.OrdinalIgnoreCase);

    private bool HasOpenWindow() =>
        _view is not null && _view.Windows.Any(IsWindowOpen);

    private void CloseWindows()
    {
        if (_view is null)
            return;
        foreach (var window in _view.Windows)
            UnmountWindow(window);
        _openEffectType = null;
        CloseAlchemyFilterMenus();
        _infoPopupAction = null;
        _infoPopupUseAction = null;
        _infoPopupSellAction = null;
        UpdateWindowLayerState();
        if (_deferredHudRefresh)
        {
            _deferredHudRefresh = false;
            ApplyStateToView();
        }
    }

    private void CloseInfoPopup()
    {
        if (_view is not null)
            UnmountWindow(_view.InfoPopup);
        _infoPopupAction = null;
        _infoPopupUseAction = null;
        _infoPopupSellAction = null;
    }

    private void ConfirmInfoPopup()
    {
        var action = _infoPopupAction;
        CloseInfoPopup();
        action?.Invoke();
    }

    private void UseInfoPopupItem()
    {
        var action = _infoPopupUseAction;
        CloseInfoPopup();
        action?.Invoke();
    }

    private void SellInfoPopupItem()
    {
        var action = _infoPopupSellAction;
        CloseInfoPopup();
        action?.Invoke();
    }

    private void ShowDeathWindow()
    {
        CloseWindows();
        var stage = database.Cultivation.Stages[_state.Character.Cultivation.StageIndex];
        _view!.DeathAge.Value = $"{Format(_state.Character.Age.TotalYears)} / {Format(cultivation.GetMaximumAge(_state.Character))} лет";
        _view.DeathStage.Value = $"{stage.Name} · ур. {_state.Character.Cultivation.Level}";
        _view.DeathYear.Value = _state.Calendar.CurrentYear.ToString(CultureInfo.InvariantCulture);
        OpenWindow(_view.DeathWindow);
    }

    private void RestartGame()
    {
        InitializeNewGame();
        _elapsedMilliseconds = 0f;
        _healthUiElapsed = 0f;
        _gameOver = false;
        _selectedInventoryItem = null;
        UnmountWindow(_view!.DeathWindow);
        Save();
        ApplyStateToView();
    }

    private void UpdateWindowLayerState()
    {
        if (_view is null)
            return;

        if (IsWindowOpen(_view.ShopWindow))
            UpdateShopWindowHeight();
        if (IsWindowOpen(_view.SettingsWindow))
            UpdateSettingsWindowHeight();

        foreach (var windowDocument in _view.WindowDocuments.All)
        {
            var hasTargetWindow = _view.Windows.Any(window =>
                ReferenceEquals(_view.GetWindowDocument(window), windowDocument) &&
                window.Classes.Contains(WindowOpenClass));
            var backdrop = windowDocument.Query<UiPanel>("#window-backdrop");
            if (backdrop is not null)
            {
                backdrop.IsVisible = true;
                if (hasTargetWindow)
                {
                    SetPaintVisibility(backdrop, true);
                    backdrop.AddClass(WindowOpenClass);
                }
                else
                {
                    backdrop.RemoveClass(WindowOpenClass);
                }
            }

            var hasVisibleSurface = _view.Windows.Any(window =>
                ReferenceEquals(_view.GetWindowDocument(window), windowDocument) &&
                IsWindowOpen(window)) || backdrop is not null && IsWindowOpen(backdrop);
            windowDocument.GetElementById<UiPanel>("window-layer")
                .SetAttribute("class", hasVisibleSurface ? "modal-active" : string.Empty);
            windowDocument.IsVisible = hasVisibleSurface;
        }
    }

    private void BuildFloatingUi(UiDocument document)
    {
        _floatingValues.Clear();
        _floatingValueIndex = 0;
        _tapFeedback = document.GetElementById<UiPanel>("tap-feedback");
        _achievementEffect = document.GetElementById<UiPanel>("achievement-effect");
        _achievementText = document.GetElementById<UiText>("achievement-text");
        var host = document.GetElementById<UiPanel>("tick-float-layer");
        host.Clear();
        for (var lane = 0; lane < 6; lane++)
        {
            var root = document.CreatePanel(new Dictionary<string, string>
            {
                ["class"] = $"tick-float lane-{lane}",
                ["animation-trigger"] = "manual", ["aria-hidden"] = "true"
            });
            var moneyIcon = (UiImage)document.CreateElement("image", new Dictionary<string, string>
            {
                ["class"] = "tick-float-money-icon",
                ["sprite"] = "Assets/Textures/GameUIAtlas.atlas#money"
            });
            SetPaintVisibility(moneyIcon, false);
            root.Add(moneyIcon);
            var valueText = document.CreateText(attributes: new Dictionary<string, string>
            {
                ["class"] = "tick-float-text"
            });
            root.Add(valueText);
            host.Add(root);
            _floatingValues.Add(new FloatingValueWidget(root, moneyIcon, valueText, lane));
        }
    }

    private void SpawnFloatingValue(decimal value, string label, string tone)
    {
        if (value == 0m || _floatingValues.Count == 0)
            return;
        var widget = _floatingValues[_floatingValueIndex];
        _floatingValueIndex = (_floatingValueIndex + 1) % _floatingValues.Count;
        widget.Root.SetAttribute("class",
            $"tick-float {tone} lane-{widget.Lane}{(value < 0m ? " negative" : string.Empty)}");
        SetPaintVisibility(widget.MoneyIcon, false);
        widget.Value.Value = tone == "money-value"
            ? MoneyFormatter.Format(decimal.ToInt64(value))
            : tone == "spirit-value"
                ? CompactNumberFormatter.Format(value, includePlusSign: true)
                : Signed(value);
        _floatingDocument?.RestartAnimation(widget.Root);
    }

    private void ShowCombatDamage(IReadOnlyList<CombatEvent> events)
    {
        if (_view is null || _document is null)
            return;
        foreach (var combatEvent in events)
        {
            UiText? target = combatEvent.Type switch
            {
                CombatEventType.HeroHurt => _view.CombatHeroDamage,
                CombatEventType.EnemyHurt => _view.CombatEnemyDamage,
                _ => null
            };
            if (target is null || combatEvent.Amount <= 0m)
                continue;
            target.Value = Signed(-combatEvent.Amount);
            _document.RestartAnimation(target);
        }
    }

    private void ShowAchievement(string text)
    {
        if (_achievementEffect is null || _achievementText is null)
            return;
        _achievementText.Value = text;
        _floatingDocument?.RestartAnimation(_achievementEffect);
    }

    private void BuildTransientUi(UiDocument document)
    {
        _actionToastHost = document.GetElementById<UiPanel>("action-toast-host");
        _actionToastHost.Clear();
        _actionToast = null;
        _actionToastIcon = null;
        _actionToastText = null;
        _actionToastExpiresAt = 0;
        _actionToastQueue.Clear();
    }

    private void ShowActionFeedback(string message, string icon, bool success, bool info = false)
    {
        var toneClass = info ? "toast-info" : success ? "toast-success" : "toast-error";
        // Keep no backlog: the latest user action replaces the previous toast and
        // therefore always disappears one lifetime after the last action.
        _actionToastQueue.Clear();
        _actionToastQueue.Enqueue(new ActionToastRequest(message, icon, toneClass));
        HideActionToast();
        ShowNextActionToast();
    }

    private void HideActionToast()
    {
        if (_actionToast is null)
            return;
        _actionToast.RemoveFromParent();
        _actionToast = null;
        _actionToastIcon = null;
        _actionToastText = null;
        _actionToastExpiresAt = 0;
    }

    private void ShowNextActionToast()
    {
        if (_actionToastHost is null || _transientDocument is null || _actionToast is not null)
            return;
        if (_actionToastQueue.Count == 0)
            return;

        var toast = _actionToastQueue.Dequeue();
        _actionToast = _transientDocument.Instantiate<UiPanel>(
            "Components/ActionToast.xml",
            _actionToastHost);
        _actionToastIcon = _actionToast.Query<UiImage>("#action-toast-icon") ??
            throw new InvalidDataException("Action toast icon is missing.");
        _actionToastText = _actionToast.Query<UiText>("#action-toast-text") ??
            throw new InvalidDataException("Action toast text is missing.");
        _actionToast.SetAttribute("class", $"action-toast {toast.ToneClass}");
        _actionToastIcon.Sprite = AtlasSprite(toast.Icon);
        _actionToastText.Value = toast.Message;
        _actionToast.SetStyle("animation", "none");
        _actionToast.SetStyle("opacity", "1");
        _actionToast.SetStyle("transform", "none");
        _actionToastExpiresAt = Environment.TickCount64 + ActionToastLifetimeMilliseconds;
    }

    private static bool TryGetGuidAttribute(UiElement element, string attributeName, out Guid value)
    {
        value = Guid.Empty;
        return element.Attributes.TryGetValue(attributeName, out var raw) && Guid.TryParse(raw, out value);
    }

    private readonly record struct ActionToastRequest(string Message, string Icon, string ToneClass);

    private void ShowItemPopup(
        ItemConfig config,
        ItemInstance? item,
        string quantity,
        string context,
        Action? action = null,
        string? actionLabel = null,
        Action? useAction = null,
        Action? sellAction = null,
        long? sellPrice = null)
    {
        var rarity = item is null ? null : database.GetRarity(item.Rarity);
        var quality = item?.Quality ?? 2.5m;
        var view = _view!;
        view.InfoPopupCard.SetAttribute("class", "info-popup-card");
        SetInfoPopupDetailVisibility(view, true);
        SetItemElement(view.InfoPopupElement, view.InfoPopupElementIcon, config.Element);
        view.InfoPopupKind.Value = ItemCategoryName(config.Category);
        view.InfoPopupTitle.Value = item is null ? config.Name : ItemDisplayName(config, item);
        view.InfoPopupDescription.Value = (item?.CustomDescription ?? config.Description) +
                                         (item is null ? string.Empty : ContaminationDescription(item.Contamination));
        view.InfoPopupEffect.Value = item is null
            ? DescribeItemEffect(config, quality)
            : DescribeItemEffect(config, item);
        view.InfoPopupStatLabel1.Value = sellPrice is null ? "КОЛИЧЕСТВО" : "ЦЕНА ПРОДАЖИ";
        view.InfoPopupPriceIcon.IsVisible = sellPrice is not null;
        view.InfoPopupStatValue1.Value = sellPrice is null ? quantity : MoneyFormatter.Format(sellPrice.Value);
        view.InfoPopupStatLabel2.Value = "ЗАГРЯЗНЕНИЕ";
        view.InfoPopupStatLabel3.Value = "РЕДКОСТЬ";
        view.InfoPopupStatValue3.Value = rarity?.DisplayName ?? "Определится при получении";
        view.InfoPopupDetails.Value = context;
        view.InfoPopupOk.Label = actionLabel ?? "ПОНЯТНО";
        _infoPopupAction = action;
        _infoPopupUseAction = useAction;
        _infoPopupSellAction = sellAction;
        view.InfoPopupUse.IsVisible = useAction is not null;
        view.InfoPopupSell.IsVisible = sellAction is not null;
        view.InfoPopupSell.Label = sellPrice is null ? "ПРОДАТЬ" : $"ПРОДАТЬ\n+{MoneyFormatter.Format(sellPrice.Value)}";
        view.InfoPopupOk.IsVisible = useAction is null && sellAction is null || action is not null;
        view.InfoPopupQuality.IsVisible = true;
        view.InfoPopupStatValue2.Value = item is null ? "—" : FormatContamination(item.Contamination);
        view.InfoPopupStatValue2.IsVisible = true;
        BuildQualityStars(view.InfoPopupQuality, item?.Quality);
        view.InfoPopupIcon.Sprite = AtlasSprite(config.Icon);
        var accent = rarity?.Color ?? "#56d5a0";
        view.InfoPopupIconWell.Style.BorderColor = accent;
        MountWindow(view.InfoPopup, exclusive: false);
    }

    private void ShowAlchemyFailurePopup()
    {
        var view = _view!;
        view.InfoPopupCard.SetAttribute("class", "info-popup-card alchemy-failure-popup");
        SetInfoPopupDetailVisibility(view, false);
        SetItemElement(view.InfoPopupElement, view.InfoPopupElementIcon, null);
        view.InfoPopupKind.Value = string.Empty;
        view.InfoPopupTitle.Value = "Неудача!";
        view.InfoPopupDescription.Value = "Все ингредиенты потеряны.";
        view.InfoPopupEffect.Value = string.Empty;
        view.InfoPopupStatLabel1.Value = string.Empty;
        view.InfoPopupStatValue1.Value = string.Empty;
        view.InfoPopupPriceIcon.IsVisible = false;
        view.InfoPopupStatLabel2.Value = string.Empty;
        view.InfoPopupStatValue2.Value = string.Empty;
        view.InfoPopupStatValue2.IsVisible = false;
        view.InfoPopupStatLabel3.Value = string.Empty;
        view.InfoPopupStatValue3.Value = string.Empty;
        view.InfoPopupDetails.Value = string.Empty;
        view.InfoPopupOk.Label = "ПОНЯТНО";
        _infoPopupAction = null;
        _infoPopupUseAction = null;
        _infoPopupSellAction = null;
        view.InfoPopupUse.IsVisible = false;
        view.InfoPopupSell.IsVisible = false;
        view.InfoPopupOk.IsVisible = true;
        view.InfoPopupQuality.IsVisible = false;
        view.InfoPopupIcon.Sprite = AtlasSprite("Assets/Textures/UIIcons/close.png");
        view.InfoPopupIconWell.Style.BorderColor = "#d85a5a";
        MountWindow(view.InfoPopup, exclusive: false);
    }

    private static void SetInfoPopupDetailVisibility(GameView view, bool isVisible)
    {
        view.InfoPopupEffect.IsVisible = isVisible;
        view.InfoPopup.Query<UiPanel>("#info-popup-stats")!.IsVisible = isVisible;
        view.InfoPopupDetails.IsVisible = isVisible;
    }

    private static void SetItemElement(UiPanel host, UiImage icon, Element? element)
    {
        host.IsVisible = element.HasValue;
        if (element is not { } value)
            return;
        icon.Sprite = AtlasSprite(ElementIcon(value));
    }

    private static string ElementIcon(Element element) => element switch
    {
        Element.Fire => "Assets/Textures/UIIcons/Elements/fire.png",
        Element.Water => "Assets/Textures/UIIcons/Elements/water.png",
        Element.Earth => "Assets/Textures/UIIcons/Elements/earth.png",
        Element.Air => "Assets/Textures/UIIcons/Elements/air.png",
        Element.Void => "Assets/Textures/UIIcons/Elements/void.png",
        _ => throw new ArgumentOutOfRangeException(nameof(element))
    };

    private void AddRewardIcon(UiElement parent, ItemConfig item, string badge)
    {
        var tile = AddRewardIcon(parent, item.Icon, badge);
        var document = _view!.GetDocumentFor(parent);
        var qualityHost = document.CreatePanel(new Dictionary<string, string>
        {
            ["class"] = "reward-quality item-icon-quality"
        });
        tile.Add(qualityHost);
        BuildQualityStars(qualityHost, null);
        tile.Clicked += _ => ShowItemPopup(item, null, badge.TrimStart('×'), "Возможная награда за миссию");
    }

    private void BuildMissionRewardPreview(UiElement parent, MissionConfig mission)
    {
        parent.Clear();
        var candidates = database.Items.Values
            .Where(item => mission.Reward.RequiredItemCategory is null || item.Category == mission.Reward.RequiredItemCategory)
            .OrderByDescending(item => item.ShopWeight)
            .ToArray();

        var canShowMoney = mission.Reward.Money > 0;
        var canShowItem = candidates.Length > 0;
        if (!canShowMoney && !canShowItem)
            return;

        var showMoney = canShowMoney;
        var showItem = canShowItem;

        if (canShowMoney && canShowItem)
        {
            var variant = Math.Abs(mission.Id.GetHashCode()) % 3;
            showMoney = variant is 0 or 2;
            showItem = variant is 1 or 2;
        }

        if (showItem)
        {
            var item = candidates[0];
            AddRewardIcon(parent, item, $"×{mission.Reward.MinimumQuantity}–{mission.Reward.MaximumQuantity}");
        }

        if (showMoney)
            AddRewardIcon(parent, "Assets/Textures/UIIcons/money.png", MoneyFormatter.Format(mission.Reward.Money));
    }

    private UiElement AddRewardIcon(UiElement parent, string source, string badge)
    {
        var document = _view!.GetDocumentFor(parent);
        var tile = document.CreateElement("panel", new Dictionary<string, string> { ["class"] = "reward-icon-tile" });
        tile.Add(document.CreateElement("image", new Dictionary<string, string>
        {
            ["class"] = "reward-item-icon",
            ["sprite"] = AtlasSprite(source)
        }));
        tile.Add(document.CreateElement("text", new Dictionary<string, string> { ["class"] = "reward-icon-badge" }, badge));
        parent.Add(tile);
        return tile;
    }

    private QualityStarsView CreateQualityStars(UiElement host)
    {
        var document = _view?.GetDocumentFor(host) ?? _document!;
        return CreateQualityStars(document, host);
    }

    private static QualityStarsView CreateQualityStars(UiDocument document, UiElement host)
    {
        var stars = document.Instantiate("Components/QualityStars.xml", host);
        return new QualityStarsView(stars);
    }

    private void BuildQualityStars(UiElement host, decimal? quality)
    {
        host.Clear();
        var stars = CreateQualityStars(host);
        if (quality is { } knownQuality)
            stars.SetQuality(knownQuality);
        else
            stars.SetUnknown();
    }

    private static string ItemDisplayName(ItemConfig config, ItemInstance item) => item.CustomName ?? config.Name;

    private static string FormatContamination(decimal contamination) =>
        $"{Math.Clamp(contamination, 0m, 1m) * 100m:0.#}%";

    private static string ContaminationDescription(decimal contamination) =>
        $"\nЗагрязнение: {FormatContamination(contamination)}";

    private static void SetContaminationBadge(UiText badge, decimal contamination)
    {
        var value = Math.Clamp(contamination, 0m, 1m);
        badge.Value = FormatContamination(value);
        badge.IsVisible = value > 0m;
        badge.ToggleClass("high-contamination", value >= 0.5m);
    }

    private string DescribeItemEffect(ItemConfig config, ItemInstance item)
    {
        IReadOnlyList<ItemEffectDefinition> definitions = item.CraftedEffects.Count > 0
            ? item.CraftedEffects
            : config.Effects;
        if (definitions.Count == 0)
        {
            var properties = alchemy.GetProperties(item);
            return properties.Count == 0
                ? "Материал для алхимии."
                : string.Join(" · ", properties.Select(value =>
                    database.GetAlchemyProperty(value.PropertyId).DisplayName));
        }
        var strength = item.CraftedEffects.Count > 0
            ? 1m
            : ItemBalanceFormula.GetEffectStrength(item, config, database);
        var effectText = string.Join("; ", definitions.Select(effect =>
            DescribeEffect(effect, strength, config.DurationType == ItemDurationType.Temporary)));
        return config.DurationType switch
        {
            ItemDurationType.Temporary => $"{effectText} на {FormatDuration(item.CraftedDurationTicks ?? config.TemporaryDurationTicks)}",
            ItemDurationType.UntilBreakthroughAttempt => $"{effectText} к следующей попытке прорыва",
            _ => effectText
        };
    }

    private string DescribeItemEffect(ItemConfig config, decimal quality)
    {
        if (config.Effects.Count == 0)
            return "Материал для алхимии.";
        // The rarity is still unknown in this preview, so show the common-rarity value.
        var strength = ItemBalanceFormula.GetQualityMultiplier(database.Balance, config.Category, quality);
        var effectText = string.Join("; ", config.Effects.Select(effect =>
            DescribeEffect(effect, strength, config.DurationType == ItemDurationType.Temporary)));
        return config.DurationType switch
        {
            ItemDurationType.Temporary => $"{effectText} на {FormatDuration(config.TemporaryDurationTicks)}",
            ItemDurationType.UntilBreakthroughAttempt => $"{effectText} к следующей попытке прорыва",
            _ => effectText
        };
    }

    private static string DescribeEffect(
        ItemEffectDefinition effect,
        decimal strength,
        bool pluralBreakthroughChance)
    {
        var value = effect.Value * strength;
        return effect.Type switch
        {
            EffectType.TickEfficiency => $"Получение духовной силы {SignedUi(value)}%",
            EffectType.AgingSpeed => $"Скорость старения {SignedUi(value)}%",
            EffectType.BreakthroughChance when pluralBreakthroughChance => $"Шансы прорыва {SignedUi(value)}%",
            EffectType.BreakthroughChance => $"Шанс прорыва {SignedUi(value)}%",
            EffectType.SpiritualPowerGain when effect.Operation == ModifierOperation.Flat => $"Добавляет {CompactNumberFormatter.Format(value)} духовной силы",
            EffectType.SpiritualPowerGain => $"Получение духовной силы {SignedUi(value)}%",
            EffectType.MissionProgress => $"Скорость выполнения миссий {SignedUi(value)}%",
            EffectType.HealthRegeneration when effect.Operation == ModifierOperation.Flat => $"Регенерация здоровья {SignedUi(value)}/с",
            EffectType.HealthRegeneration => $"Регенерация здоровья {SignedUi(value)}%",
            EffectType.MaximumHealth when effect.Operation == ModifierOperation.Flat => $"Максимум здоровья {SignedUi(value)}",
            EffectType.MaximumHealth => $"Максимум здоровья {SignedUi(value)}%",
            EffectType.Attack when effect.Operation == ModifierOperation.Flat => $"Атака {SignedUi(value)}",
            EffectType.Attack => $"Атака {SignedUi(value)}%",
            EffectType.AttackSpeed => $"Скорость атаки {SignedUi(value)}%",
            EffectType.HealthRestore => $"Восстанавливает {Format(value)} здоровья",
            EffectType.LongevityYears => $"Предел жизни {SignedUi(value)} лет",
            EffectType.PurifyContamination => $"Очищает от загрязнения на {Format(value)}%",
            _ => $"Эффект {SignedUi(value)}%"
        };
    }

    private string FormatDuration(int weeks)
    {
        var years = weeks / _state.Calendar.TicksPerYear;
        var remainder = weeks % _state.Calendar.TicksPerYear;
        if (years == 0)
            return FormatWeeks(remainder);
        if (remainder == 0)
            return $"{years} {Plural(years, "год", "года", "лет")}";
        return $"{years} {Plural(years, "год", "года", "лет")} {FormatWeeks(remainder)}";
    }

    private static string FormatWeeks(int weeks) => $"{weeks} {Plural(weeks, "неделя", "недели", "недель")}";

    private static string Plural(int number, string one, string few, string many)
    {
        var lastTwo = Math.Abs(number) % 100;
        if (lastTwo is >= 11 and <= 14) return many;
        return (Math.Abs(number) % 10) switch { 1 => one, 2 or 3 or 4 => few, _ => many };
    }

    private static string ItemCategoryName(ItemCategory category) => category switch
    {
        ItemCategory.Pill => "ПИЛЮЛЯ", ItemCategory.Core => "ЯДРО ЗВЕРЯ", ItemCategory.Ingredient => "ИНГРЕДИЕНТ", _ => category.ToString().ToUpperInvariant()
    };

    private static string EffectName(EffectType type) => type switch
    {
        EffectType.TickEfficiency => "Эффективность культивации", EffectType.AgingSpeed => "Старение",
        EffectType.BreakthroughChance => "Шанс прорыва", EffectType.SpiritualPowerGain => "Духовная сила",
        EffectType.MissionProgress => "Выполнение миссий",
        EffectType.HealthRegeneration => "Регенерация здоровья",
        EffectType.MaximumHealth => "Максимум здоровья",
        EffectType.Attack => "Атака",
        EffectType.AttackSpeed => "Скорость атаки",
        EffectType.HealthRestore => "Исцеление",
        EffectType.Contamination => "Загрязнение",
        EffectType.LongevityYears => "Предел жизни",
        EffectType.PurifyContamination => "Очищение",
        _ => type.ToString()
    };

    private int GetContaminationLevelNumber() => GetContaminationLevelNumber(_state.Character.Contamination);

    private int GetContaminationLevelNumber(decimal contamination) => database.Balance.ContaminationLevels
        .OrderBy(level => level.MinimumContamination)
        .ToList()
        .FindIndex(level => level.MinimumContamination == ContaminationCalculator.GetLevel(contamination, database.Balance)?.MinimumContamination) + 1;

    private void BindClick(UiButton button, Action action) => button.Clicked += _ =>
    {
        PlaySound("Sounds/ui-click.wav", 0.45f);
        action();
    };

    private void PlaySound(string path, float volume)
    {
        if (!_state.Settings.SoundsEnabled)
            return;
        try { audio.Play(path, volume: volume); } catch { }
    }

    private static string Format(decimal value)
    {
        if (value != 0m && Math.Abs(value) < 1m)
        {
            var smallValue = Math.Round(value, 2, MidpointRounding.AwayFromZero);
            return smallValue.ToString("0.00", CultureInfo.InvariantCulture);
        }

        return Math.Round(value, MidpointRounding.AwayFromZero)
            .ToString("0", CultureInfo.InvariantCulture);
    }

    private static string CssPixels(float value) =>
        string.Concat(value.ToString("0.##", CultureInfo.InvariantCulture), "px");

    private static string SignedUi(decimal value)
    {
        var rounded = value != 0m && Math.Abs(value) < 1m
            ? Math.Round(value, 2, MidpointRounding.AwayFromZero)
            : Math.Round(value, MidpointRounding.AwayFromZero);
        return rounded > 0m ? $"+{Format(value)}" : Format(value);
    }

    // Floating tick/tap values intentionally keep their fractional precision.
    private static string Signed(decimal value) =>
        value >= 0m
            ? $"+{value.ToString("0.#", CultureInfo.InvariantCulture)}"
            : value.ToString("0.#", CultureInfo.InvariantCulture);

    private static string AtlasSprite(string source)
    {
        var normalized = source.Replace('\\', '/');
        var atlas = normalized.Contains("/Items/", StringComparison.OrdinalIgnoreCase)
            ? "Assets/Textures/GameUIAtlas.atlas"
            : "Assets/Textures/GameUIAtlas.atlas";
        return $"{atlas}#{Path.GetFileNameWithoutExtension(normalized)}";
    }

    private static void SetPaintVisibility(UiElement element, bool visible) =>
        element.Style.Set("visibility", visible ? "visible" : "hidden");

    private sealed record FloatingValueWidget(UiPanel Root, UiImage MoneyIcon, UiText Value, int Lane);
    private sealed record AlchemySlotWidget(
        UiButton Root,
        UiImage Icon,
        UiImage ElementIcon,
        UiPanel QualityHost,
        QualityStarsView Quality,
        UiText Label);
}
