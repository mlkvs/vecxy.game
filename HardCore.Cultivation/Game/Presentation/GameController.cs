using System.Globalization;
using System.Numerics;
using System.Collections.Generic;
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
    TickProcessor ticks,
    MissionService missions,
    ShopService shop,
    ShopTransactionService transactions,
    ItemEffectService effects,
    ItemPriceCalculator prices,
    CultivationService cultivation,
    AlchemyService alchemy,
    DogMeditationService dogMeditation,
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
    private UiDocument? _transientDocument;
    private GameView? _view;
    private UiPanel? _windowLayer;
    private UiPanel? _actionToast;
    private UiImage? _actionToastIcon;
    private UiText? _actionToastText;
    private Action? _infoPopupAction;
    private Action? _infoPopupUseAction;
    private Action? _infoPopupSellAction;
    private float _actionToastRemaining;
    private float _actionToastDuration;
    private readonly Queue<ActionToastRequest> _actionToastQueue = new();
    private UiPanel? _tapFeedback;
    private UiPanel? _achievementEffect;
    private UiText? _achievementText;
    private GameState _state = null!;
    private float _elapsedMilliseconds;
    private bool _gameOver;
    private ItemCategory _inventoryCategory = ItemCategory.Ingredient;
    private Guid? _selectedInventoryItem;
    private readonly List<Guid?> _alchemySlots = [];
    private Guid? _alchemyCore;
    private AlchemyMode _alchemyMode;
    private int _alchemyRarityFilter;
    private int _alchemyQualityFilter;
    private int _alchemyTypeFilter;
    private EffectType? _openEffectType;
    private readonly Dictionary<(string Tone, bool Negative), List<UiText>> _floatingValuePools = [];
    private readonly Dictionary<(string Tone, bool Negative), int> _floatingValueIndices = [];
    private readonly Dictionary<EffectType, UiRadialProgress> _effectWidgets = [];
    private UiKeyedCollection<Guid, ShopSlot, ShopCardView>? _shopCards;
    private UiKeyedCollection<Guid, ItemInstance, InventoryIconView>? _inventoryIcons;
    private UiKeyedCollection<string, string, MissionCardView>? _missionCards;
    private UiKeyedCollection<Guid, ActiveMission, MissionQueueItemView>? _missionQueueItems;
    private UiText? _missionBoardEmpty;
    private UiText? _missionQueueEmpty;
    private UiText? _shopEmpty;
    private decimal _pendingHealthRestored;
    private float _healthFloatElapsed;
    private DogCompanion? _dog;
    private bool _dogConfigured;
    private Rect? _dogTapBounds;

    public GameState State => _state;
    public event Action<TickResult>? TickCompleted;

    public void Initialize()
    {
        if (!saves.TryLoad(out _state))
            InitializeNewGame();
        if (_state.Shop.Slots.Count == 0)
            shop.Refresh(_state.Shop);
        if (_state.MissionBoard.MissionIds.Count == 0)
            missions.Refresh(_state);
        combat.ConfigureHero(_state.Character, _state.Character.MaximumHealth <= 0m);
        combatScene.Initialize();
        SyncDogVisual();
        _gameOver = _state.Character.Age.TotalYears >= cultivation.GetMaximumAge(_state.Character);

        _floatingDocument = ui.Load("UI/FloatingOverlay.xml");
        _floatingDocument.Reloaded += BuildFloatingUi;
        BuildFloatingUi(_floatingDocument);
        _document = ui.Load("UI/Main.xml");
        _document.Reloaded += BuildUi;
        BuildUi(_document);
        _transientDocument = ui.Load("UI/TransientOverlay.xml");
        _transientDocument.Reloaded += BuildTransientUi;
        BuildTransientUi(_transientDocument);
    }

    public void Update(float deltaTime)
    {
        if (_actionToast is not null)
        {
            if (_actionToast.IsVisible)
            {
                _actionToastRemaining -= deltaTime;
                UpdateActionToastVisuals();
                if (_actionToastRemaining <= 0f)
                {
                    HideActionToast();
                    ShowNextActionToast();
                }
            }
            else if (_actionToastQueue.Count > 0)
            {
                ShowNextActionToast();
            }
        }

        if (_gameOver)
            return;

        if (dogMeditation.Update(_state, deltaTime))
            Save();
        SyncDogVisual();

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
            if (combatUpdate.Events.Any(value => value.Type == CombatEventType.Victory))
                ShowAchievement("ПОБЕДА");
            if (combatUpdate.Events.Any(value => value.Type == CombatEventType.Defeat))
                ShowAchievement("ПОРАЖЕНИЕ");
            if (combatUpdate.Events.Any(value => value.Type == CombatEventType.Closed))
            {
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
            UpdateHud();
            _pendingHealthRestored += combatUpdate.HealthRestored;
            _healthFloatElapsed += deltaTime;
            if (_healthFloatElapsed >= 1f || combatUpdate.RecoveryCompleted)
            {
                SpawnFloatingValue(_pendingHealthRestored, "HP", "health-value");
                _pendingHealthRestored = 0m;
                _healthFloatElapsed = 0f;
            }
        }
        if (combatUpdate.RecoveryCompleted)
        {
            ShowAchievement("МОЖНО ВЕРНУТЬСЯ К МИССИЯМ");
            UpdateActivityButtons();
            Save();
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

    public void Save() => saves.Save(_state);

    public void Dispose()
    {
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
        if (_transientDocument is not null)
        {
            _transientDocument.Reloaded -= BuildTransientUi;
            ui.Unload(_transientDocument);
        }
        _document = null;
        _floatingDocument = null;
        _transientDocument = null;
        _view = null;
        _shopCards = null;
        _inventoryIcons = null;
        _missionCards = null;
        _missionQueueItems = null;
        _shopEmpty = null;
        _dog = null;
        _dogConfigured = false;
        _dogTapBounds = null;
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

    private void BuildUi(UiDocument document)
    {
        var layer = document.GetElementById<UiPanel>("window-layer");
        _windowLayer = layer;
        foreach (var child in layer.Children.ToArray())
        {
            if (child.Id != "window-backdrop")
                child.RemoveFromParent();
        }
        document.Instantiate("Components/ShopWindow.xml", layer);
        document.Instantiate("Components/InventoryWindow.xml", layer);
        document.Instantiate("Components/AlchemyWindow.xml", layer);
        document.Instantiate("Components/MissionsWindow.xml", layer);
        document.Instantiate("Components/DeathWindow.xml", layer);
        document.Instantiate("Components/BreakthroughWindow.xml", layer);
        document.Instantiate("Components/BreakthroughResult.xml", layer);
        document.Instantiate("Components/EffectPopup.xml", layer);
        document.Instantiate("Components/InfoPopup.xml", layer);
        _view = new GameView(document);
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
        _missionBoardEmpty = null;
        _missionQueueEmpty = null;
        _shopEmpty = null;

        _dogTapBounds = null;
        ResetAlchemySlots();
        _alchemyCore = null;

        BindClick(_view.ShopButton, () => { OpenWindow(_view.ShopWindow); SyncShop(); });
        BindClick(_view.AlchemyButton, OpenAlchemy);
        BindClick(_view.InventoryButton, () => { OpenWindow(_view.InventoryWindow); SyncInventory(); });
        BindClick(_view.MissionSummaryButton, OpenMissions);
        BindClick(_view.ActivityMode, ToggleActivityMode);
        BindClick(_view.Breakthrough, OpenBreakthrough);
        BindClick(_view.ConfirmBreakthrough, AttemptBreakthrough);
        BindClick(_view.CancelBreakthrough, () => UnmountWindow(_view.BreakthroughWindow));
        BindClick(_view.BreakthroughResultOk, () => UnmountWindow(_view.BreakthroughResult));
        BindClick(_view.Restart, RestartGame);
        BindClick(_view.InfoPopupOk, ConfirmInfoPopup);
        BindClick(_view.InfoPopupUse, UseInfoPopupItem);
        BindClick(_view.InfoPopupSell, SellInfoPopupItem);
        BindClick(_view.InfoPopupClose, CloseInfoPopup);
        BindClick(_view.EffectPopupClose, CloseEffectPopup);
        _view.EffectPopup.Clicked += _ => CloseEffectPopup();
        _view.CharacterTapTarget.ClickedAt += (_, position) => TapCharacter(position);
        BindClick(_view.DogTapTarget, ReactDog);
        BindClick(_view.AvailableMissionsTab, () => ShowMissionPage(false));
        BindClick(_view.AcceptedMissionsTab, () => ShowMissionPage(true));
        BindClick(_view.IngredientsTab, () => SelectInventoryCategory(ItemCategory.Ingredient));
        BindClick(_view.CoresTab, () => SelectInventoryCategory(ItemCategory.Core));
        BindClick(_view.PillsTab, () => SelectInventoryCategory(ItemCategory.Pill));
        BindClick(_view.AlchemyPillTab, () => SetAlchemyMode(AlchemyMode.Pill));
        BindClick(_view.AlchemyDistillTab, () => SetAlchemyMode(AlchemyMode.Distillation));
        BindClick(_view.AlchemyRarityFilter, () => ToggleAlchemyFilterMenu(_view.AlchemyRarityMenu));
        BindClick(_view.AlchemyQualityFilter, () => ToggleAlchemyFilterMenu(_view.AlchemyQualityMenu));
        BindClick(_view.AlchemyTypeFilter, () => ToggleAlchemyFilterMenu(_view.AlchemyTypeMenu));
        BindClick(_view.AlchemyCraft, CraftAlchemy);
        BindClick(_view.InventoryUse, UseSelectedItem);
        BindClick(_view.InventorySell, SellSelectedItem);
        _view.WindowBackdrop.Clicked += _ => { };
        foreach (var close in _view.WindowCloseButtons)
            close.Clicked += _ => CloseWindows();

        BuildAlchemyFilterMenus();
        ApplyStateToView();
        if (_gameOver)
            ShowDeathWindow();
        else
            CloseWindows();
    }

    private void ProcessWeek()
    {
        var moneyBefore = _state.Character.Money;
        var result = ticks.ProcessTick(_state);
        if (result.MissionCompleted)
        {
            PlaySound("Sounds/mission-complete.wav", 0.65f);
            ShowAchievement("МИССИЯ ВЫПОЛНЕНА");
        }
        if (result.LevelsGained > 0)
        {
            PlaySound("Sounds/cultivate.wav", 0.6f);
            ShowAchievement(result.LevelsGained == 1 ? "НОВЫЙ УРОВЕНЬ" : $"+{result.LevelsGained} УРОВНЯ");
        }
        if (result.CharacterDied)
        {
            _gameOver = true;
            PlaySound("Sounds/death.wav", 0.7f);
        }
        if (result.TickNumber % database.Balance.AutoSaveEveryTicks == 0)
            Save();

        ApplyStateToView();
        if (_view!.ShopWindow.IsVisible || result.NewYearStarted)
            SyncShop();
        if (_view.InventoryWindow.IsVisible || result.MissionCompleted)
            SyncInventory();
        if (_view.MissionsWindow.IsVisible)
            SyncMissions();
        if (_openEffectType is not null && _view.EffectPopup.IsVisible)
            UpdateEffectPopup();
        if (result.SpiritualPowerGained != 0m)
            SpawnFloatingValue(result.SpiritualPowerGained, string.Empty, "spirit-value");
        if (result.MissionProgressAdded != 0m)
            SpawnFloatingValue(result.MissionProgressAdded, "ПРОГРЕСС", "mission-value");
        var moneyDelta = _state.Character.Money - moneyBefore;
        if (moneyDelta != 0)
            SpawnFloatingValue(moneyDelta, "РУБ.", "money-value");
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
        if (result.SpiritualPowerGained != 0m)
            SpawnFloatingValue(result.SpiritualPowerGained, string.Empty, "spirit-value");
        if (result.LevelsGained > 0)
        {
            ShowAchievement(result.LevelsGained == 1 ? "НОВЫЙ УРОВЕНЬ" : $"+{result.LevelsGained} УРОВНЯ");
            ApplyStateToView();
        }
        else
        {
            UpdateHud();
        }
    }

    private void ReactDog()
    {
        var result = dogMeditation.Collect(_state);
        SyncDogVisual();
        if (!result.Success)
        {
            var progress = Math.Round(dogMeditation.GetProgress(_state) * 100f);
            ShowActionFeedback(
                $"Собака медитирует: {progress:0}%",
                "Assets/Textures/UIIcons/money.png",
                true,
                info: true);
            return;
        }

        PlaySound("Sounds/item.wav", 0.55f);
        SpawnFloatingValue(result.Reward, "РУБ.", "money-value");
        ShowActionFeedback(result.Message, "Assets/Textures/UIIcons/money.png", true);
        UpdateHud();
        Save();
    }

    private void SyncDogVisual()
    {
        _dog ??= scenes.ActiveScene?.Objects
            .Select(sceneObject => sceneObject.GetComponent<DogCompanion>())
            .FirstOrDefault(component => component is not null);
        if (_dog is not null && !_dogConfigured)
        {
            _dog.Configure(database.Dog);
            _dogConfigured = true;
        }
        _dog?.SetChargeProgress(dogMeditation.GetProgress(_state));
        SyncDogTapTarget();
    }

    private void SyncDogTapTarget()
    {
        if (_dog is null || _view is null || renderer.GameOutputWidth <= 0 || renderer.GameOutputHeight <= 0)
            return;

        var camera = scenes.ActiveScene?.Objects
            .Select(sceneObject => sceneObject.GetComponent<Camera>())
            .FirstOrDefault(component => component is { TargetTexture: null });
        var root = _view.Document.Root.Bounds;
        var parent = _view.DogTapTarget.Parent?.Bounds ?? default;
        var aspectRatio = renderer.GameOutputWidth / (float)renderer.GameOutputHeight;
        if (camera is null || root.Width <= 0f || root.Height <= 0f ||
            parent.Width <= 0f || parent.Height <= 0f ||
            !_dog.TryGetViewportBounds(camera, aspectRatio, out var viewportBounds))
            return;

        var projectedLeft = viewportBounds.Left * root.Width;
        var projectedTop = viewportBounds.Top * root.Height;
        var projectedRight = viewportBounds.Right * root.Width;
        var projectedBottom = viewportBounds.Bottom * root.Height;
        var clippedLeft = Math.Clamp(projectedLeft, parent.Left, parent.Right);
        var clippedTop = Math.Clamp(projectedTop, parent.Top, parent.Bottom);
        var clippedRight = Math.Clamp(projectedRight, parent.Left, parent.Right);
        var clippedBottom = Math.Clamp(projectedBottom, parent.Top, parent.Bottom);

        var left = MathF.Floor(clippedLeft - parent.Left);
        var top = MathF.Floor(clippedTop - parent.Top);
        var right = MathF.Ceiling(clippedRight - parent.Left);
        var bottom = MathF.Ceiling(clippedBottom - parent.Top);
        var replacement = new Rect(left, top, MathF.Max(0f, right - left), MathF.Max(0f, bottom - top));
        if (_dogTapBounds == replacement)
            return;

        _dogTapBounds = replacement;
        _view.DogTapTarget.Style.Set("left", CssPixels(replacement.Left));
        _view.DogTapTarget.Style.Set("top", CssPixels(replacement.Top));
        _view.DogTapTarget.Style.Set("width", CssPixels(replacement.Width));
        _view.DogTapTarget.Style.Set("height", CssPixels(replacement.Height));
    }

    private void SetActivity(ActivityMode mode)
    {
        if (mode == ActivityMode.Missions && _state.RecoveryRequired)
        {
            ShowActionFeedback(
                "После поражения восстановите здоровье до отмеченного порога.",
                "Assets/Textures/UIIcons/close.png",
                false);
            return;
        }
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

    private void UpdateHud()
    {
        var character = _state.Character;
        var progress = character.Cultivation;
        var stage = database.Cultivation.Stages[progress.StageIndex];
        var required = cultivation.GetRequiredPower(progress.StageIndex, progress.Level);
        var powerBars = required <= 0m ? 1m : Math.Max(0m, character.SpiritualPower / required);
        _view!.YearDial.Progress = 1f - _state.Calendar.TickInYear / (float)_state.Calendar.TicksPerYear;
        _view.Money.Value = character.Money.ToString("N0", CultureInfo.InvariantCulture);
        _view.Age.Value = Format(character.Age.TotalYears);
        _view.MaximumAge.Value = Format(cultivation.GetMaximumAge(character));
        _view.Realm.Value = $"{stage.Name} · ур. {progress.Level}";
        UpdateCultivationPowerBar(character.SpiritualPower, required, powerBars);
        var healthFraction = character.MaximumHealth <= 0m ? 0m : character.Health / character.MaximumHealth;
        _view.HeroHealthProgress.Progress = (float)Math.Clamp(healthFraction, 0m, 1m);
        _view.HeroRecoveryThreshold.IsVisible = _state.RecoveryRequired;
        _view.HeroRecoveryThreshold.Style.Set("left", string.Concat(
            (database.Combat.RecoveryHealthFraction * 100m).ToString("0.##", CultureInfo.InvariantCulture), "%"));
        _view.HeroHealthText.Value = $"{Format(character.Health)} / {Format(character.MaximumHealth)}";
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
            _view.CultivationProgressText.Value = $"{Format(spiritualPower)} / {Format(required)}";
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
            ? $"{Format(spiritualPower)} / {Format(required)} · запас +{Format(reservePercent)}%"
            : $"{Format(spiritualPower)} / {Format(required)}";
    }

    private void UpdateActivityButtons()
    {
        if (_view is null)
            return;
        if (_state.RecoveryRequired)
        {
            _view.ActivityMode.SetAttribute("class", "activity-toggle recovery");
            _view.ActivityModeIcon.Sprite = AtlasSprite("Assets/Textures/UIIcons/cultivation.png");
            _view.ActivityModeText.Value = "ВОССТАНОВЛЕНИЕ";
            return;
        }
        _view.ActivityMode.SetAttribute("class", _state.ActivityMode == ActivityMode.Missions
            ? "activity-toggle missions"
            : "activity-toggle");
        _view.ActivityModeIcon.Sprite = AtlasSprite(_state.ActivityMode == ActivityMode.Missions
            ? "Assets/Textures/UIIcons/missions.png"
            : "Assets/Textures/UIIcons/cultivation.png");
        _view.ActivityModeText.Value = _state.ActivityMode == ActivityMode.Missions
            ? "МИССИИ"
            : "КУЛЬТИВАЦИЯ";
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
            _view.MissionNormalState.IsVisible = true;
            _view.MissionCombatState.IsVisible = false;
            return;
        }
        var config = database.GetMission(mission.MissionConfigId);
        _view!.MissionName.Value = config.Name;
        _view.MissionDescription.Value = _state.RecoveryRequired
            ? "Ожидает полного восстановления"
            : _state.ActivityMode == ActivityMode.Missions
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
        _view.MissionCombatStatus.Value = active.Phase switch
        {
            CombatPhase.Victory => "ПОБЕДА",
            CombatPhase.Defeat => "ПОРАЖЕНИЕ",
            _ => $"АВТОБОЙ · {monster.Name}"
        };
        var danger = database.GetDanger(active.DangerLevel);
        _view.MissionCombatStats.Value =
            $"АТК {Format(monster.Attack * danger.MonsterPowerMultiplier)} · " +
            $"ЗАЩ {Format(monster.Defense * danger.MonsterPowerMultiplier)} · " +
            $"СКОР {Format((decimal)monster.AttacksPerSecond)}/с";
        _view.EnemyHealthProgress.Progress = (float)(active.EnemyMaximumHealth <= 0m
            ? 0m
            : Math.Clamp(active.EnemyHealth / active.EnemyMaximumHealth, 0m, 1m));
        _view.EnemyHealthText.Value = $"{Format(active.EnemyHealth)} / {Format(active.EnemyMaximumHealth)}";
    }

    private void SyncEffects()
    {
        var groups = _state.ActiveEffects.Where(effect => !effect.IsExpired)
            .GroupBy(effect => effect.Type).OrderBy(group => group.Key).ToArray();
        var signature = string.Join('|', groups.SelectMany(group => group.Select(effect =>
            $"{effect.Type}:{effect.SourceItemId}:{effect.Value}:{effect.DurationType}")));
        _view!.Effects.IsVisible = groups.Length > 0;
        var currentSignature = _view!.Effects.Attributes.GetValueOrDefault("data-signature");
        if (signature != currentSignature)
        {
            _view.Effects.SetAttribute("data-signature", signature);
            _view.Effects.Clear();
            _effectWidgets.Clear();
            if (groups.Length == 0)
                return;
            foreach (var group in groups)
            {
                var source = database.GetItem(group.First().SourceItemId);
                var orb = _document!.CreateButton(attributes: new Dictionary<string, string> { ["class"] = "effect-orb" });
                var ring = (UiRadialProgress)_document.CreateElement("radial-progress", new Dictionary<string, string> { ["class"] = "effect-ring" });
                orb.Add(ring);
                var effectIcon = _document.CreateImage(source.Icon, new Dictionary<string, string> { ["class"] = "effect-icon" });
                effectIcon.Sprite = AtlasSprite(source.Icon);
                orb.Add(effectIcon);
                var type = group.Key;
                orb.Clicked += _ => ShowEffectPopup(type);
                _view.Effects.Add(orb);
                _effectWidgets[type] = ring;
            }
        }
        foreach (var group in groups)
            if (_effectWidgets.TryGetValue(group.Key, out var ring))
                ring.Progress = CalculateEffectTimer(group.ToArray());
    }

    private void SyncShop()
    {
        if (_view is null || _shopCards is null)
            return;
        _view.ShopMoney.Value = $"{_state.Character.Money:N0} рублей";
        var availableSlots = _state.Shop.Slots.Where(slot => slot.AvailableQuantity > 0).ToArray();
        _shopCards.Update(availableSlots, slot => slot.SlotId);
        if (availableSlots.Length == 0)
        {
            if (_shopEmpty is null)
            {
                _shopEmpty = _document!.CreateText(
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

    private ShopCardView CreateShopCard(ShopSlot slot)
    {
        var root = _document!.Instantiate("Components/ShopCard.xml", _view!.ShopGrid, new Dictionary<string, string>
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
        card.Effect.Value = DescribeItemEffect(config, slot.Item);
        card.Buy.Label = $"{unitPrice.ToString(CultureInfo.InvariantCulture)} РУБЛЕЙ";
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
            $"Цена покупки: {unitPrice:N0} рублей");
    }

    private void BuyShopItem(Guid slotId)
    {
        var slot = _state.Shop.Slots.FirstOrDefault(candidate => candidate.SlotId == slotId);
        if (slot is not null)
            Buy(slotId, database.GetItem(slot.Item.ConfigId));
    }

    private void Buy(Guid slotId, ItemConfig config)
    {
        var result = transactions.Buy(_state, slotId);
        ShowActionFeedback(result.Success ? $"Куплено: {config.Name} · −{result.TotalPrice:N0} руб." : result.Message,
            result.Success ? config.Icon : "Assets/Textures/UIIcons/close.png", result.Success);
        if (result.Success)
        {
            SpawnFloatingValue(-result.TotalPrice, "РУБ.", "money-value");
            UpdateHud();
            SyncShop();
            SyncInventory();
        }
    }

    private void SelectInventoryCategory(ItemCategory category)
    {
        _inventoryCategory = category;
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
        _inventoryIcons.Update(
            _state.Inventory.Items.Where(item => database.GetItem(item.ConfigId).Category == _inventoryCategory),
            item => item.InstanceId);
        UpdateInventorySelection();
        if (_selectedInventoryItem is { } selected && _state.Inventory.Find(selected) is not null)
            SelectInventoryItem(selected);
        else
            _view.InventoryDetails.IsVisible = false;
    }

    private InventoryIconView CreateInventoryIcon(ItemInstance item)
    {
        var root = _document!.Instantiate("Components/InventoryIcon.xml", _view!.InventoryGrid, new Dictionary<string, string>
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
        _view.InventoryDetailEffect.Value = DescribeItemEffect(config, item);
        _view.InventoryUse.IsEnabled = config.Effects.Count > 0 || item.CraftedEffects.Count > 0;
        _view.InventorySell.Label = $"ПРОДАТЬ\n+{prices.GetSellPrice(item, _state.Shop)} РУБЛЕЙ";
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
        var before = _state.Character.SpiritualPower;
        var result = effects.Use(_state, id);
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
        ShowActionFeedback(result.Success ? $"Продано: {ItemDisplayName(config, item)} · +{result.TotalPrice:N0} руб." : result.Message,
            result.Success ? "Assets/Textures/UIIcons/money.png" : "Assets/Textures/UIIcons/close.png", result.Success);
        if (result.Success)
        {
            SpawnFloatingValue(result.TotalPrice, "РУБ.", "money-value");
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
        ResetAlchemySlots();
        _alchemyCore = null;
        _alchemyMode = AlchemyMode.Pill;
        CloseAlchemyFilterMenus();
        OpenWindow(_view!.AlchemyWindow);
        SyncAlchemy();
    }

    private void SetAlchemyMode(AlchemyMode mode)
    {
        _alchemyMode = mode;
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

        BuildAlchemySlots();
        BuildAlchemyIngredients();
        var preview = alchemy.Preview(_state, CurrentAlchemySelection(), _alchemyMode);
        _view.AlchemyCraft.IsEnabled = preview.CanCraft;
        _view.AlchemyCraft.Label = _alchemyMode == AlchemyMode.Pill ? "СОЗДАТЬ ПИЛЮЛЮ" : "РАФИНИРОВАТЬ";
    }

    private void BuildAlchemySlots()
    {
        _view!.AlchemySelection.Clear();
        _view.AlchemySelection.Add(_document!.CreateImage(
            "Assets/Textures/UI/alchemy-room.png",
            new Dictionary<string, string> { ["class"] = "alchemy-room" }));
        var furnaceStage = _document.CreatePanel(new Dictionary<string, string>
        {
            ["class"] = "alchemy-furnace-stage"
        });
        furnaceStage.Add(_document.CreateImage(
            "Assets/Textures/UI/alchemy-furnace.png",
            new Dictionary<string, string> { ["class"] = "alchemy-furnace" }));
        _view.AlchemySelection.Add(furnaceStage);
        EnsureAlchemySlots();
        for (var index = 0; index < database.Alchemy.MaximumIngredients; index++)
        {
            var selectedUnit = _alchemySlots[index];
            var slot = _document!.CreateButton(attributes: new Dictionary<string, string>
            {
                ["class"] = selectedUnit is not null
                    ? $"alchemy-slot alchemy-outer-slot slot-{index + 1} filled"
                    : $"alchemy-slot alchemy-outer-slot slot-{index + 1}"
            });
            if (selectedUnit is { } instanceId)
            {
                var item = _state.Inventory.Find(instanceId)!;
                var config = database.GetItem(item.ConfigId);
                var image = _document.CreateImage(config.Icon);
                image.Sprite = AtlasSprite(config.Icon);
                slot.Add(image);
                var qualityHost = _document.CreatePanel(new Dictionary<string, string>
                {
                    ["class"] = "alchemy-slot-quality item-icon-quality"
                });
                slot.Add(qualityHost);
                BuildQualityStars(qualityHost, item.Quality);
                var slotIndex = index;
                slot.Clicked += _ => RemoveAlchemyIngredientAt(slotIndex);
            }
            else
            {
                slot.Add(_document.CreateText((index + 1).ToString(CultureInfo.InvariantCulture),
                    new Dictionary<string, string> { ["class"] = "alchemy-slot-index" }));
            }
            furnaceStage.Add(slot);
        }

        var coreSlot = _document!.CreateButton(attributes: new Dictionary<string, string>
        {
            ["class"] = _alchemyMode == AlchemyMode.Distillation
                ? "alchemy-slot alchemy-core-slot equipment"
                : _alchemyCore is null
                    ? "alchemy-slot alchemy-core-slot"
                    : "alchemy-slot alchemy-core-slot filled"
        });
        if (_alchemyMode == AlchemyMode.Distillation)
        {
            coreSlot.Add(_document.CreateElement("image", new Dictionary<string, string>
            {
                ["sprite"] = "Assets/Textures/UIIconsAtlas.atlas#alchemy"
            }));
        }
        else if (_alchemyCore is { } coreId && _state.Inventory.Find(coreId) is { } core)
        {
            var config = database.GetItem(core.ConfigId);
            var image = _document.CreateImage(config.Icon);
            image.Sprite = AtlasSprite(config.Icon);
            coreSlot.Add(image);
            var qualityHost = _document.CreatePanel(new Dictionary<string, string>
            {
                ["class"] = "alchemy-slot-quality item-icon-quality"
            });
            coreSlot.Add(qualityHost);
            BuildQualityStars(qualityHost, core.Quality);
            coreSlot.Clicked += _ =>
            {
                _alchemyCore = null;
                SyncAlchemy();
            };
        }
        else
        {
            coreSlot.Add(_document.CreateText("ЯДРО", new Dictionary<string, string> { ["class"] = "alchemy-core-label" }));
        }
        furnaceStage.Add(coreSlot);
    }

    private void BuildAlchemyIngredients()
    {
        _view!.AlchemyIngredients.Clear();
        foreach (var item in _state.Inventory.Items
                     .Where(item =>
                     {
                         var category = database.GetItem(item.ConfigId).Category;
                         return _alchemyMode == AlchemyMode.Pill
                             ? category == ItemCategory.Core || category == ItemCategory.Ingredient && alchemy.GetProperties(item).Count > 0
                             : category == ItemCategory.Ingredient && alchemy.GetProperties(item).Count > 0;
                     })
                     .Where(MatchesAlchemyFilters)
                     .OrderBy(item => database.GetItem(item.ConfigId).Category == ItemCategory.Core ? 0 : 1)
                     .ThenByDescending(item => item.DistillationLevel)
                     .ThenByDescending(item => item.Rarity)
                     .ThenByDescending(item => item.Quality))
        {
            var config = database.GetItem(item.ConfigId);
            var isCore = config.Category == ItemCategory.Core;
            var selected = isCore
                ? (_alchemyCore == item.InstanceId ? 1 : 0)
                : _alchemySlots.Count(value => value == item.InstanceId);
            var root = _document!.Instantiate("Components/InventoryIcon.xml", _view.AlchemyIngredients,
                new Dictionary<string, string>
                {
                    ["key"] = item.InstanceId.ToString(), ["icon"] = string.Empty,
                    ["quantity"] = item.Quantity.ToString(CultureInfo.InvariantCulture)
                });
            var icon = new InventoryIconView(root);
            icon.QualityStars = CreateQualityStars(icon.QualityHost);
            icon.Icon.Sprite = AtlasSprite(config.Icon);
            icon.QualityStars.SetQuality(item.Quality);
            icon.Quantity.Value = $"×{item.Quantity}";
            icon.IconWell.Style.BorderColor = database.GetRarity(item.Rarity).Color;
            icon.Card.ToggleClass("selected", selected > 0);
            var instanceId = item.InstanceId;
            root.Clicked += _ => ShowAlchemyItem(instanceId);
        }
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
        var option = _document!.CreateButton(label, new Dictionary<string, string>
        {
            ["class"] = "alchemy-filter-option",
            ["data-filter-value"] = value.ToString(CultureInfo.InvariantCulture)
        });
        option.Clicked += _ =>
        {
            select(value);
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
        _view!.AlchemyRarityFilter.Label = $"РЕДКОСТЬ: {rarityLabel} ▼";
        _view.AlchemyQualityFilter.Label = $"КАЧЕСТВО: {qualityLabel} ▼";
        _view.AlchemyTypeFilter.Label = $"ТИП: {typeLabel} ▼";
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
        var result = alchemy.Craft(_state, CurrentAlchemySelection(), _alchemyMode);
        if (!result.Success || result.Output is not { } output)
        {
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
        var sellPrice = prices.GetSellPrice(output, _state.Shop);
        var canUse = config.Effects.Count > 0 || output.CraftedEffects.Count > 0;
        ShowItemPopup(
            config,
            output,
            "1",
            mode == AlchemyMode.Pill ? "Создано в алхимической печи." : "Получено после рафинирования.",
            useAction: canUse ? () => UseInventoryItem(output.InstanceId) : null,
            sellAction: () => SellInventoryItem(output.InstanceId),
            sellPrice: sellPrice);
    }

    private void OpenMissions()
    {
        OpenWindow(_view!.MissionsWindow);
        SyncMissions();
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
                _missionBoardEmpty = _document!.CreateText(
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
        var root = _document!.Instantiate("Components/MissionCard.xml", _view!.MissionsList, new Dictionary<string, string>
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
        card.Danger.IsVisible = mission.DangerLevel is not null;
        card.Danger.Value = mission.DangerLevel is { } danger
            ? $"ОПАСНОСТЬ {new string('I', danger)}"
            : string.Empty;
        card.Duration.Value = $"{mission.MinimumDurationTicks}–{mission.MaximumDurationTicks} недель";
        card.Start.IsEnabled = _state.MissionQueue.Count < database.Balance.MaximumMissionQueueSize;
    }

    private void StartMission(string missionId)
    {
        var result = missions.Start(_state, missionId);
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
                _missionQueueEmpty = _document!.CreateText(
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
        var root = _document!.Instantiate("Components/MissionQueueItem.xml", _view!.MissionQueue, new Dictionary<string, string>
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
            $"Нужно: {Format(required)} · накоплено: {Format(_state.Character.SpiritualPower)}\nПосле успеха запас обнулится";
        OpenWindow(_view.BreakthroughWindow);
    }

    private void AttemptBreakthrough()
    {
        UnmountWindow(_view!.BreakthroughWindow);
        var result = cultivation.AttemptBreakthrough(_state.Character, _state.ActiveEffects);
        combat.ConfigureHero(_state.Character);
        _view.BreakthroughResultTitle.Value = result.Success ? "ПРОРЫВ УСПЕШЕН" : "ПРОРЫВ НЕ УДАЛСЯ";
        _view.BreakthroughResultText.Value = result.Success
            ? "Вы перешли на новую ступень культивации."
            : $"Прорыв не удался, вы получили травму и потеряли {result.LevelsLost} уровней";
        OpenWindow(_view.BreakthroughResult);
        if (result.Success)
        {
            PlaySound("Sounds/breakthrough.wav", 0.7f);
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
        var active = _state.ActiveEffects.Where(effect => effect.Type == type).ToArray();
        if (active.Length == 0)
        {
            _view!.EffectPopupEffect.Value = $"{EffectName(type)}: осталось 0 недель";
            return;
        }
        var duration = active.Any(effect => effect.IsUntilBreakthroughAttempt)
            ? "к следующей попытке прорыва"
            : active.All(effect => effect.IsPermanent)
                ? string.Empty
                : $"на {FormatDuration(active.Where(effect => !effect.IsPermanent).Min(effect => Math.Max(0, effect.RemainingTicks ?? 0)))}";
        var description = string.Join("; ", active.Select(effect => DescribeEffect(
            new ItemEffectDefinition { Type = effect.Type, Operation = effect.Operation, Value = effect.Value },
            1m,
            effect.DurationType == ItemDurationType.Temporary)));
        _view!.EffectPopupEffect.Value = $"{description}{(string.IsNullOrEmpty(duration) ? string.Empty : $" · {duration}")}";
    }

    private void CloseEffectPopup()
    {
        if (_view is not null)
            UnmountWindow(_view.EffectPopup);
        _openEffectType = null;
    }

    private float CalculateEffectTimer(IReadOnlyList<ActiveEffect> active)
    {
        if (active.Any(effect => effect.IsUntilBreakthroughAttempt) || active.All(effect => effect.IsPermanent))
            return 1f;
        return (float)active.Where(effect => !effect.IsPermanent).Min(effect =>
        {
            var total = Math.Max(1, database.GetItem(effect.SourceItemId).TemporaryDurationTicks);
            return Math.Clamp((effect.RemainingTicks ?? 0) / (decimal)total, 0m, 1m);
        });
    }

    private void OpenWindow(UiPanel window)
    {
        MountWindow(window, exclusive: true);
    }

    private void MountWindow(UiPanel window, bool exclusive)
    {
        if (exclusive)
            CloseWindows();
        if (window.Parent is null)
            _windowLayer!.Add(window);
        window.IsVisible = true;
        UpdateWindowLayerState();
    }

    private void UnmountWindow(UiPanel window)
    {
        window.IsVisible = false;
        window.DetachFromParent();
        UpdateWindowLayerState();
    }

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

        var hasOpenWindow = _view.Windows.Any(window => window.Parent is not null && window.IsVisible);
        _view.WindowLayer.SetAttribute("class", hasOpenWindow ? "modal-active" : string.Empty);
        if (hasOpenWindow)
        {
            if (_view.WindowBackdrop.Parent is null)
                _view.WindowLayer.Insert(0, _view.WindowBackdrop);
            _view.WindowBackdrop.IsVisible = true;
        }
        else
        {
            _view.WindowBackdrop.IsVisible = false;
            _view.WindowBackdrop.DetachFromParent();
        }
    }

    private void BuildFloatingUi(UiDocument document)
    {
        _floatingValuePools.Clear();
        _floatingValueIndices.Clear();
        _tapFeedback = document.GetElementById<UiPanel>("tap-feedback");
        _achievementEffect = document.GetElementById<UiPanel>("achievement-effect");
        _achievementText = document.GetElementById<UiText>("achievement-text");
        var host = document.GetElementById<UiPanel>("tick-float-layer");
        host.Clear();
        var lane = 0;
        foreach (var tone in new[] { "spirit-value", "mission-value", "money-value", "health-value" })
        foreach (var negative in new[] { false, true })
        {
            var key = (tone, negative);
            var pool = new List<UiText>(3);
            _floatingValuePools[key] = pool;
            for (var index = 0; index < 3; index++)
            {
                var element = document.CreateText(attributes: new Dictionary<string, string>
                {
                    ["class"] = $"tick-float {tone} lane-{lane++ % 6}{(negative ? " negative" : string.Empty)}",
                    ["animation-trigger"] = "manual", ["aria-hidden"] = "true"
                });
                host.Add(element);
                pool.Add(element);
            }
        }
    }

    private void SpawnFloatingValue(decimal value, string label, string tone)
    {
        if (value == 0m || !_floatingValuePools.TryGetValue((tone, value < 0m), out var pool) || pool.Count == 0)
            return;
        var key = (tone, value < 0m);
        var sequence = _floatingValueIndices.GetValueOrDefault(key);
        _floatingValueIndices[key] = sequence + 1;
        var element = pool[Random.Shared.Next(pool.Count)];
        element.Value = string.IsNullOrEmpty(label) ? Signed(value) : $"{Signed(value)} {label}";
        _floatingDocument?.RestartAnimation(element);
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
        _actionToast = document.GetElementById<UiPanel>("action-toast");
        _actionToastIcon = document.GetElementById<UiImage>("action-toast-icon");
        _actionToastText = document.GetElementById<UiText>("action-toast-text");
        _actionToastRemaining = 0f;
        _actionToastDuration = 0f;
        _actionToastQueue.Clear();
        if (_actionToast is not null)
            HideActionToast();
    }

    private void ShowActionFeedback(string message, string icon, bool success, bool info = false)
    {
        var toneClass = info ? "toast-info" : success ? "toast-success" : "toast-error";
        _actionToastQueue.Enqueue(new ActionToastRequest(message, icon, toneClass));
        if (_actionToast is not null && !_actionToast.IsVisible)
            ShowNextActionToast();
    }

    private void HideActionToast()
    {
        if (_actionToast is null)
            return;
        _actionToastRemaining = 0f;
        _actionToastDuration = 0f;
        _actionToast.IsVisible = false;
        _actionToast.SetAttribute("aria-hidden", "true");
        _actionToast.SetStyle("opacity", "0");
        _actionToast.SetStyle("animation", "none");
        _actionToast.SetStyle("transform", "translate(0, -18px)");
    }

    private void ShowNextActionToast()
    {
        if (_actionToast is null || _actionToastIcon is null || _actionToastText is null)
            return;
        if (_actionToastQueue.Count == 0)
            return;

        var toast = _actionToastQueue.Dequeue();
        _actionToast.SetAttribute("class", $"action-toast {toast.ToneClass}");
        _actionToastIcon.Sprite = AtlasSprite(toast.Icon);
        _actionToastText.Value = toast.Message;
        _actionToastDuration = 1.85f;
        _actionToastRemaining = _actionToastDuration;
        _actionToast.RemoveAttribute("hidden");
        _actionToast.SetAttribute("aria-hidden", "false");
        _actionToast.SetStyle("animation", "none");
        _actionToast.IsVisible = true;
        UpdateActionToastVisuals();
    }

    private void UpdateActionToastVisuals()
    {
        if (_actionToast is null || !_actionToast.IsVisible || _actionToastDuration <= 0f)
            return;

        var elapsed = Math.Clamp(_actionToastDuration - _actionToastRemaining, 0f, _actionToastDuration);
        const float fadeIn = 0.16f;
        const float fadeOut = 0.22f;
        var opacity = 1f;
        var offset = 0f;

        if (elapsed < fadeIn)
        {
            var t = elapsed / fadeIn;
            opacity = t;
            offset = -18f * (1f - t);
        }
        else if (_actionToastRemaining < fadeOut)
        {
            var t = Math.Max(0f, _actionToastRemaining / fadeOut);
            opacity = t;
            offset = -12f * (1f - t);
        }

        _actionToast.SetStyle("opacity", opacity.ToString("0.###", CultureInfo.InvariantCulture));
        _actionToast.SetStyle("transform", $"translate(0, {offset:0.###}px)");
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
        view.InfoPopupKind.Value = ItemCategoryName(config.Category);
        view.InfoPopupTitle.Value = item is null ? config.Name : ItemDisplayName(config, item);
        view.InfoPopupDescription.Value = item?.CustomDescription ?? config.Description;
        view.InfoPopupEffect.Value = item is null
            ? DescribeItemEffect(config, quality)
            : DescribeItemEffect(config, item);
        view.InfoPopupStatLabel1.Value = sellPrice is null ? "КОЛИЧЕСТВО" : "ЦЕНА ПРОДАЖИ";
        view.InfoPopupStatValue1.Value = sellPrice is null ? quantity : $"{sellPrice:N0} РУБЛЕЙ";
        view.InfoPopupStatLabel2.Value = "КАЧЕСТВО";
        view.InfoPopupStatLabel3.Value = "РЕДКОСТЬ";
        view.InfoPopupStatValue3.Value = rarity?.DisplayName ?? "Определится при получении";
        view.InfoPopupDetails.Value = context;
        view.InfoPopupOk.Label = actionLabel ?? "ПОНЯТНО";
        _infoPopupAction = action;
        _infoPopupUseAction = useAction;
        _infoPopupSellAction = sellAction;
        view.InfoPopupUse.IsVisible = useAction is not null;
        view.InfoPopupSell.IsVisible = sellAction is not null;
        view.InfoPopupSell.Label = sellPrice is null ? "ПРОДАТЬ" : $"ПРОДАТЬ\n+{sellPrice:N0} РУБЛЕЙ";
        view.InfoPopupOk.IsVisible = useAction is null && sellAction is null || action is not null;
        view.InfoPopupQuality.IsVisible = true;
        view.InfoPopupStatValue2.IsVisible = false;
        BuildQualityStars(view.InfoPopupQuality, item?.Quality);
        view.InfoPopupIcon.Sprite = AtlasSprite(config.Icon);
        var accent = rarity?.Color ?? "#56d5a0";
        view.InfoPopupKind.Style.Color = accent;
        view.InfoPopupIconWell.Style.BorderColor = accent;
        MountWindow(view.InfoPopup, exclusive: false);
    }

    private void AddRewardIcon(UiElement parent, ItemConfig item, string badge)
    {
        var tile = AddRewardIcon(parent, item.Icon, badge);
        var qualityHost = _document!.CreatePanel(new Dictionary<string, string>
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
            AddRewardIcon(parent, "Assets/Textures/UIIcons/money.png", $"{mission.Reward.Money}");
    }

    private UiElement AddRewardIcon(UiElement parent, string source, string badge)
    {
        var tile = _document!.CreateElement("panel", new Dictionary<string, string> { ["class"] = "reward-icon-tile" });
        tile.Add(_document.CreateElement("image", new Dictionary<string, string>
        {
            ["class"] = "reward-item-icon",
            ["sprite"] = AtlasSprite(source)
        }));
        tile.Add(_document.CreateElement("text", new Dictionary<string, string> { ["class"] = "reward-icon-badge" }, badge));
        parent.Add(tile);
        return tile;
    }

    private QualityStarsView CreateQualityStars(UiElement host)
    {
        var stars = _document!.Instantiate("Components/QualityStars.xml", host);
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

    private string DescribeItemEffect(ItemConfig config, ItemInstance item)
    {
        var definitions = item.CraftedEffects.Count > 0 ? item.CraftedEffects : config.Effects;
        if (definitions.Count == 0)
        {
            var properties = alchemy.GetProperties(item);
            return properties.Count == 0
                ? "Материал для алхимии."
                : string.Join(" · ", properties.Select(value =>
                    $"{database.GetAlchemyProperty(value.PropertyId).DisplayName} {value.Potency:0.##}"));
        }
        var strength = database.Balance.EffectQualityBase + item.Quality * database.Balance.EffectQualityPerPoint;
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
        var strength = database.Balance.EffectQualityBase + quality * database.Balance.EffectQualityPerPoint;
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
            EffectType.SpiritualPowerGain when effect.Operation == ModifierOperation.Flat => $"Добавляет {Format(value)} духовной силы",
            EffectType.SpiritualPowerGain => $"Получение духовной силы {SignedUi(value)}%",
            EffectType.MissionProgress => $"Скорость выполнения миссий {SignedUi(value)}%",
            EffectType.HealthRegeneration when effect.Operation == ModifierOperation.Flat => $"Регенерация здоровья {SignedUi(value)}/с",
            EffectType.HealthRegeneration => $"Регенерация здоровья {SignedUi(value)}%",
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
        _ => type.ToString()
    };

    private void BindClick(UiButton button, Action action) => button.Clicked += _ =>
    {
        PlaySound("Sounds/ui-click.wav", 0.45f);
        action();
    };

    private void PlaySound(string path, float volume)
    {
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
            ? "Assets/Textures/ItemsIconsAtlas.atlas"
            : "Assets/Textures/UIIconsAtlas.atlas";
        return $"{atlas}#{Path.GetFileNameWithoutExtension(normalized)}";
    }
}
