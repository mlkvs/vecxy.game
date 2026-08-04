using System.Globalization;
using System.Numerics;
using HardCore.Cultivation.Game.Application;
using HardCore.Cultivation.Game.Domain;
using HardCore.Cultivation.Game.Infrastructure;
using Vecxy.Audio;
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
    IAudioManager audio,
    GameSaveSystem saves)
{
    private UiDocument? _document;
    private UiDocument? _floatingDocument;
    private UiDocument? _transientDocument;
    private GameView? _view;
    private UiPanel? _actionToast;
    private UiImage? _actionToastIcon;
    private UiText? _actionToastText;
    private UiPanel? _tapFeedback;
    private GameState _state = null!;
    private float _elapsedMilliseconds;
    private string? _missionQueueSignature;
    private string? _effectsSignature;
    private string? _cultivationPathSignature;
    private UiText? _activeMissionProgress;
    private UiElement? _currentPathNode;
    private readonly Dictionary<EffectType, EffectWidgets> _effectWidgets = [];
    private readonly Dictionary<Guid, ShopCardView> _shopCards = [];
    private readonly Dictionary<Guid, InventoryCardView> _inventoryCards = [];
    private readonly Dictionary<string, MissionCardView> _missionCards = [];
    private readonly Dictionary<(string Tone, bool Negative), List<UiText>> _floatingValuePools = [];
    private readonly Dictionary<(string Tone, bool Negative), int> _floatingValueIndices = [];
    private UiText? _missionBoardEmpty;
    private bool _gameOver;
    private bool _alternateActionToast;
    private bool _centerCultivationPathPending;
    private bool _shopDirty = true;
    private bool _inventoryDirty = true;
    private bool _missionsDirty = true;
    private bool _cultivationPathDirty = true;
    private Action? _effectItemAction;

    private sealed record EffectWidgets(
        UiRadialProgress Ring,
        UiText Value,
        UiText Stack);

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
        _gameOver = _state.Character.Age.TotalYears >= database.Balance.MaximumAgeYears;

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
        if (_centerCultivationPathPending)
            TryCenterCultivationPath();
        if (_gameOver)
            return;
        _elapsedMilliseconds += deltaTime * 1000f;
        var processed = 0;
        while (_elapsedMilliseconds >= database.Balance.RealMillisecondsPerTick && processed++ < 100)
        {
            _elapsedMilliseconds -= database.Balance.RealMillisecondsPerTick;
            ProcessTick();
            if (_gameOver)
                break;
        }
    }

    public void Save() => saves.Save(_state);

    public void Dispose()
    {
        if (_document is null)
            return;
        _document.Reloaded -= BuildUi;
        ui.Unload(_document);
        if (_floatingDocument is not null)
        {
            _floatingDocument.Reloaded -= BuildFloatingUi;
            ui.Unload(_floatingDocument);
            _floatingDocument = null;
        }
        if (_transientDocument is not null)
        {
            _transientDocument.Reloaded -= BuildTransientUi;
            ui.Unload(_transientDocument);
            _transientDocument = null;
        }
        _document = null;
        _view = null;
    }

    private void InitializeNewGame()
    {
        _state = new GameState(database.Balance.TicksPerYear);
        _state.Character.Restore(0m, 0, database.Balance.StartingAgeYears);
        _state.Character.AddMoney(database.Balance.StartingMoney);
        shop.Refresh(_state.Shop);
        missions.Refresh(_state);
        missions.Start(_state, _state.MissionBoard.MissionIds[0]);
    }

    private void BuildUi(UiDocument document)
    {
        _missionQueueSignature = null;
        _effectsSignature = null;
        _cultivationPathSignature = null;
        _activeMissionProgress = null;
        _currentPathNode = null;
        _effectWidgets.Clear();
        _shopCards.Clear();
        _inventoryCards.Clear();
        _missionCards.Clear();
        _missionBoardEmpty = null;
        var layer = document.GetElementById<UiPanel>("window-layer");
        layer.Clear();
        document.Instantiate("Components/ShopWindow.xml", layer);
        document.Instantiate("Components/InventoryWindow.xml", layer);
        document.Instantiate("Components/CultivationWindow.xml", layer);
        document.Instantiate("Components/MissionsWindow.xml", layer);
        document.Instantiate("Components/DeathWindow.xml", layer);
        document.Instantiate("Components/EffectPopup.xml", layer);
        document.Instantiate("Components/InfoPopup.xml", layer);
        _view = new GameView(document);

        BindClick(_view.ShopButton, () => OpenDataWindow(_view.ShopWindow));
        BindClick(_view.InventoryButton, () => OpenDataWindow(_view.InventoryWindow));
        BindClick(_view.CultivationButton, OpenCultivationWindow);
        BindClick(_view.MissionsButton, () => OpenDataWindow(_view.MissionsWindow));
        BindClick(_view.Advance, AdvanceLevel);
        BindClick(_view.Breakthrough, AttemptBreakthrough);
        BindClick(_view.DetailAdvance, AdvanceLevel);
        BindClick(_view.DetailBreakthrough, AttemptBreakthrough);
        BindClick(_view.Restart, RestartGame);
        BindClick(_view.InfoPopupOk, CloseInfoPopup);
        BindClick(_view.InfoPopupClose, CloseInfoPopup);
        BindClick(_view.EffectPopupOk, CloseEffectPopup);
        BindClick(_view.EffectPopupClose, CloseEffectPopup);
        _view.EffectPopupIconWell.Clicked += _ => _effectItemAction?.Invoke();
        _view.EffectPopupItem.Clicked += _ => _effectItemAction?.Invoke();
        _view.CharacterTapTarget.Clicked += _ => TapCharacter();
        foreach (var close in _view.WindowCloseButtons)
            close.Clicked += _ =>
            {
                PlaySound("Sounds/ui-click.wav", 0.45f);
                CloseWindows();
            };
        ApplyStateToView();
        if (_gameOver)
            ShowDeathWindow();
    }

    private void ProcessTick()
    {
        var moneyBefore = _state.Character.Money;
        var result = ticks.ProcessTick(_state);
        if (result.MissionCompleted)
            PlaySound("Sounds/mission-complete.wav", 0.65f);
        if (result.CharacterDied)
        {
            _gameOver = true;
            PlaySound("Sounds/death.wav", 0.7f);
        }
        if (result.TickNumber % database.Balance.AutoSaveEveryTicks == 0)
            Save();
        UpdateHudAndCultivation();
        UpdateMissionSummary();
        SyncEffects();
        SyncMissionQueue();
        UpdateMissionQueueProgress();
        UpdateMissionRefresh();
        if (result.MissionCompleted)
        {
            SyncInventory();
            SyncShop();
            SyncMissionBoard();
        }
        if (result.NewYearStarted)
        {
            SyncShop();
            SyncMissionBoard();
        }
        if (_gameOver)
        {
            Save();
            ShowDeathWindow();
        }
        if (result.SpiritualPowerGained != 0m)
            SpawnFloatingValue(result.SpiritualPowerGained, "СИЛЫ", "spirit-value");
        if (result.MissionProgressAdded != 0m && _state.CurrentMission is not null)
            SpawnFloatingValue(result.MissionProgressAdded, "ПРОГРЕСС", "mission-value");
        var moneyDelta = _state.Character.Money - moneyBefore;
        if (moneyDelta != 0)
            SpawnFloatingValue(moneyDelta, "МОНЕТ", "money-value");
        TickCompleted?.Invoke(result);
    }

    private void TapCharacter()
    {
        PlaySound("Sounds/cultivate.wav", 0.35f);
        if (_floatingDocument is not null && _tapFeedback is not null)
            _floatingDocument.RestartAnimation(_tapFeedback);
        ProcessTick();
    }

    private void AdvanceLevel()
    {
        var before = _state.Character.SpiritualPower;
        var result = cultivation.TryAdvanceLevel(_state.Character);
        ShowActionFeedback(result.Message, "Assets/Textures/UIIcons/cultivation.png", result.Success);
        if (result.Success)
        {
            PlaySound("Sounds/cultivate.wav", 0.6f);
            SpawnFloatingValue(_state.Character.SpiritualPower - before, "СИЛЫ", "spirit-value");
        }
        UpdateHudAndCultivation();
        SyncCultivationPath();
    }

    private void AttemptBreakthrough()
    {
        var before = _state.Character.SpiritualPower;
        var result = cultivation.AttemptBreakthrough(_state.Character, _state.ActiveEffects);
        ShowActionFeedback(result.Message, "Assets/Textures/UIIcons/cultivation.png", result.Success);
        if (result.Success)
            PlaySound("Sounds/breakthrough.wav", 0.7f);
        if (_state.Character.SpiritualPower != before)
            SpawnFloatingValue(_state.Character.SpiritualPower - before, "СИЛЫ", "spirit-value");
        UpdateHudAndCultivation();
        SyncCultivationPath();
    }

    private void ApplyStateToView()
    {
        if (_view is null)
            return;
        UpdateHudAndCultivation();
        UpdateMissionSummary();
        SyncEffects();
        _shopDirty = true;
        _inventoryDirty = true;
        _missionsDirty = true;
        _cultivationPathDirty = true;
    }

    private void UpdateHudAndCultivation()
    {
        if (_view is null)
            return;
        var character = _state.Character;
        var progress = character.Cultivation;
        var stage = database.Cultivation.Stages[progress.StageIndex];
        var required = cultivation.GetRequiredPower(progress.StageIndex, progress.Level);
        var fraction = required <= 0m ? 1m : Math.Clamp(character.SpiritualPower / required, 0m, 1m);

        _view.StageName.Value = stage.Name;
        _view.Year.Value = $"ГОД {_state.Calendar.CurrentYear}";
        _view.Tick.Value = $"ТАКТ {_state.Calendar.TickInYear} / {_state.Calendar.TicksPerYear}";
        _view.Money.Value = character.Money.ToString("N0", CultureInfo.InvariantCulture);
        _view.Spirit.Value = Format(character.SpiritualPower);
        _view.Age.Value = $"{character.Age.TotalYears:0.0} / {database.Balance.MaximumAgeYears:0}";
        _view.Realm.Value = $"{stage.Name} · {progress.Level}";
        _view.CultivationCost.Value = progress.Level == 10
            ? $"Для прорыва нужно {Format(required)} духовной силы"
            : $"Нужно {Format(required)} духовной силы до уровня {progress.Level + 1}";
        _view.CultivationProgress.Progress = (float)fraction;
        _view.Advance.IsEnabled = progress.Level < 10 && character.SpiritualPower >= required;
        _view.Breakthrough.IsEnabled = progress.CanAttemptBreakthrough &&
                                      progress.StageIndex < database.Cultivation.Stages.Count - 1 &&
                                      character.SpiritualPower >= required;

        if (_view.CultivationWindow.IsVisible)
        {
            _view.DetailStage.Value = stage.Name;
            _view.DetailLevel.Value = $"Уровень {progress.Level} из 10";
            _view.DetailCost.Value = Format(required);
            _view.DetailProgress.Progress = (float)fraction;
            _view.DetailAdvance.IsEnabled = progress.Level < 10 && character.SpiritualPower >= required;
            _view.DetailBreakthrough.IsEnabled = progress.CanAttemptBreakthrough &&
                                                progress.StageIndex < database.Cultivation.Stages.Count - 1 &&
                                                character.SpiritualPower >= required;
        }
    }

    private void UpdateMissionSummary()
    {
        var mission = _state.CurrentMission;
        if (mission is null)
        {
            _view!.MissionName.Value = "Нет активной миссии";
            _view.MissionDescription.Value = "Выберите поручение в списке миссий.";
            _view.MissionProgressText.Value = "0 / 0";
            _view.MissionProgress.Progress = 0f;
            return;
        }
        var config = database.GetMission(mission.MissionConfigId);
        var remaining = Math.Max(0m, mission.RequiredProgress - mission.CurrentProgress);
        _view!.MissionName.Value = config.Name;
        _view.MissionDescription.Value = mission.IsCompleted ? "Завершено. Можно выбрать новую миссию." : config.Description;
        _view.MissionProgressText.Value =
            $"{Format(mission.CurrentProgress)} / {Format(mission.RequiredProgress)} · осталось {Format(remaining)}";
        _view.MissionProgress.Progress = (float)(mission.RequiredProgress == 0m
            ? 1m
            : mission.CurrentProgress / mission.RequiredProgress);
    }

    private void SyncEffects()
    {
        var list = _view!.Effects;
        var groups = _state.ActiveEffects
            .Where(effect => !effect.IsExpired)
            .GroupBy(effect => effect.Type)
            .OrderBy(group => group.Key)
            .Select(group => (Type: group.Key, Effects: group.ToArray()))
            .ToArray();
        var signature = string.Join('|', groups.SelectMany(group => group.Effects.Select(effect =>
            $"{group.Type}:{effect.SourceItemId}:{effect.Operation}:{effect.Value}:{effect.IsPermanent}")));
        if (signature != _effectsSignature)
        {
            _effectsSignature = signature;
            _effectWidgets.Clear();
            list.Clear();
            if (groups.Length == 0)
            {
                list.Add(_document!.CreateText("Нет активных эффектов", new Dictionary<string, string>
                {
                    ["class"] = "detail-text"
                }));
                return;
            }
            foreach (var group in groups)
            {
                var source = database.GetItem(group.Effects[0].SourceItemId);
                var hasPermanent = group.Effects.Any(effect => effect.IsPermanent);
                var orb = _document!.CreateButton(attributes: new Dictionary<string, string>
                {
                    ["class"] = hasPermanent
                        ? "effect-orb permanent-effect"
                        : "effect-orb temporary-effect"
                });
                var ring = (UiRadialProgress)_document.CreateElement("radial-progress", new Dictionary<string, string>
                {
                    ["class"] = "effect-ring"
                });
                var icon = _document.CreateImage(source.Icon, new Dictionary<string, string>
                {
                    ["class"] = "effect-icon"
                });
                var value = _document.CreateText(attributes: new Dictionary<string, string>
                {
                    ["class"] = "effect-value-badge"
                });
                var stack = _document.CreateText(attributes: new Dictionary<string, string>
                {
                    ["class"] = "effect-stack-badge"
                });
                orb.Add(ring);
                orb.Add(icon);
                orb.Add(value);
                orb.Add(stack);
                list.Add(orb);
                var effectType = group.Type;
                orb.Clicked += _ => ShowEffectPopup(effectType);
                _effectWidgets[group.Type] = new EffectWidgets(ring, value, stack);
            }
        }

        foreach (var group in groups)
        {
            var total = CalculateEffectTotal(group.Type, group.Effects);
            var widgets = _effectWidgets[group.Type];
            widgets.Ring.Progress = CalculateEffectTimer(group.Effects);
            widgets.Value.Value = EffectValue(group.Type, total);
            widgets.Stack.Value = group.Effects.Length > 1 ? $"×{group.Effects.Length}" : string.Empty;
        }
    }

    private void SyncShop()
    {
        if (!_view!.ShopWindow.IsVisible)
        {
            _shopDirty = true;
            return;
        }
        _shopDirty = false;
        var grid = _view.ShopGrid;
        _view.ShopMarkup.Value = $"Наценка: +{_state.Shop.BuyMarkupPercent}%";
        _view.ShopMoney.Value = $"{_state.Character.Money:N0} монет";

        var liveSlots = _state.Shop.Slots.Select(slot => slot.SlotId).ToHashSet();
        foreach (var stale in _shopCards.Keys.Where(id => !liveSlots.Contains(id)).ToArray())
        {
            _shopCards[stale].Card.RemoveFromParent();
            _shopCards.Remove(stale);
        }

        foreach (var slot in _state.Shop.Slots)
        {
            var config = database.GetItem(slot.Item.ConfigId);
            var unitPrice = prices.GetBuyPrice(slot.Item, _state.Shop);
            if (!_shopCards.TryGetValue(slot.SlotId, out var widgets))
            {
                var rarity = database.GetRarity(slot.Item.Rarity);
                var card = _document!.Instantiate("Components/ShopCard.xml", grid, new Dictionary<string, string>
                {
                    ["key"] = slot.SlotId.ToString(),
                    ["icon"] = config.Icon,
                    ["rarity"] = rarity.DisplayName,
                    ["name"] = config.Name,
                    ["description"] = config.Description,
                    ["effect"] = DescribeItemEffect(config, slot.Item),
                    ["meta"] = string.Empty,
                    ["price"] = unitPrice.ToString(CultureInfo.InvariantCulture)
                });
                var cardView = new ShopCardView(card);
                card.SetStyle("border-color", rarity.Color);
                cardView.Rarity.Style.BackgroundColor = rarity.Color;
                BuildQualityStars(cardView.Quality, slot.Item.Quality);
                cardView.IconWell.Clicked += _ => ShowItemPopup(
                    config,
                    slot.Item,
                    slot.AvailableQuantity.ToString(CultureInfo.InvariantCulture),
                    $"Лавка · цена покупки {prices.GetBuyPrice(slot.Item, _state.Shop):N0} монет");
                var button = cardView.Buy;
                var slotId = slot.SlotId;
                button.Clicked += _ =>
                {
                    var result = transactions.Buy(_state, slotId);
                    PlaySound(result.Success ? "Sounds/item.wav" : "Sounds/ui-click.wav", 0.55f);
                    ShowActionFeedback(
                        result.Success ? $"Куплено: {config.Name} · −{result.TotalPrice:N0} монет" : result.Message,
                        result.Success ? config.Icon : "Assets/Textures/UIIcons/close.png",
                        result.Success);
                    if (result.Success)
                        SpawnFloatingValue(-result.TotalPrice, "МОНЕТ", "money-value");
                    if (result.Success)
                    {
                        UpdateHudAndCultivation();
                        SyncShop();
                        SyncInventory();
                    }
                };
                widgets = cardView;
                _shopCards.Add(slot.SlotId, widgets);
            }

            widgets.Meta.Value = $"В наличии: {slot.AvailableQuantity}";
            widgets.Buy.Label = $"КУПИТЬ · {unitPrice}";
            widgets.Buy.IsEnabled = slot.AvailableQuantity > 0 && _state.Character.Money >= unitPrice;
        }
    }

    private void SyncInventory()
    {
        if (!_view!.InventoryWindow.IsVisible)
        {
            _inventoryDirty = true;
            return;
        }
        _inventoryDirty = false;
        var grid = _view.InventoryGrid;
        _view.InventoryCount.Value = $"{_state.Inventory.Items.Sum(item => item.Quantity)} предметов";
        _view.SellRate.Value = $"Продажа: {_state.Shop.SellAdjustmentPercent}%";

        var liveItems = _state.Inventory.Items.Select(item => item.InstanceId).ToHashSet();
        foreach (var stale in _inventoryCards.Keys.Where(id => !liveItems.Contains(id)).ToArray())
        {
            _inventoryCards[stale].Card.RemoveFromParent();
            _inventoryCards.Remove(stale);
        }

        foreach (var item in _state.Inventory.Items)
        {
            var config = database.GetItem(item.ConfigId);
            var sellPrice = prices.GetSellPrice(item, _state.Shop);
            if (!_inventoryCards.TryGetValue(item.InstanceId, out var widgets))
            {
                var rarity = database.GetRarity(item.Rarity);
                var card = _document!.Instantiate("Components/InventoryCard.xml", grid, new Dictionary<string, string>
                {
                    ["key"] = item.InstanceId.ToString(),
                    ["icon"] = config.Icon,
                    ["rarity"] = rarity.DisplayName,
                    ["name"] = config.Name,
                    ["description"] = config.Description,
                    ["effect"] = DescribeItemEffect(config, item),
                    ["meta"] = string.Empty,
                    ["price"] = sellPrice.ToString(CultureInfo.InvariantCulture)
                });
                var cardView = new InventoryCardView(card);
                card.SetStyle("border-color", rarity.Color);
                cardView.Rarity.Style.BackgroundColor = rarity.Color;
                BuildQualityStars(cardView.Quality, item.Quality);
                cardView.IconWell.Clicked += _ => ShowItemPopup(
                    config,
                    item,
                    item.Quantity.ToString(CultureInfo.InvariantCulture),
                    $"Рюкзак · цена продажи {prices.GetSellPrice(item, _state.Shop):N0} монет");
                var itemId = item.InstanceId;
                var use = cardView.Use;
                use.Clicked += _ =>
                {
                    var spiritBefore = _state.Character.SpiritualPower;
                    var result = effects.Use(_state, itemId);
                    PlaySound(result.Success ? "Sounds/item.wav" : "Sounds/ui-click.wav", 0.55f);
                    ShowActionFeedback(result.Message, result.Success ? config.Icon : "Assets/Textures/UIIcons/close.png", result.Success);
                    if (result.Success && _state.Character.SpiritualPower != spiritBefore)
                        SpawnFloatingValue(_state.Character.SpiritualPower - spiritBefore, "СИЛЫ", "spirit-value");
                    if (result.Success)
                    {
                        UpdateHudAndCultivation();
                        SyncInventory();
                        SyncEffects();
                        SyncShop();
                    }
                };
                var sell = cardView.Sell;
                sell.Clicked += _ =>
                {
                    var result = transactions.Sell(_state, itemId);
                    PlaySound(result.Success ? "Sounds/item.wav" : "Sounds/ui-click.wav", 0.5f);
                    ShowActionFeedback(
                        result.Success ? $"Продано: {config.Name} · +{result.TotalPrice:N0} монет" : result.Message,
                        result.Success ? "Assets/Textures/UIIcons/money.png" : "Assets/Textures/UIIcons/close.png",
                        result.Success);
                    if (result.Success)
                        SpawnFloatingValue(result.TotalPrice, "МОНЕТ", "money-value");
                    if (result.Success)
                    {
                        UpdateHudAndCultivation();
                        SyncInventory();
                        SyncShop();
                    }
                };
                widgets = cardView;
                _inventoryCards.Add(item.InstanceId, widgets);
            }

            widgets.Meta.Value = $"Количество: {item.Quantity}";
            widgets.Use.IsEnabled = config.Effects.Count > 0;
            widgets.Sell.Label = $"ПРОДАТЬ · {sellPrice}";
        }
    }

    private void SyncMissionQueue()
    {
        if (!_view!.MissionsWindow.IsVisible)
        {
            _missionsDirty = true;
            return;
        }
        var signature = string.Join('|', _state.MissionQueue.Select(mission => mission.InstanceId));
        if (signature == _missionQueueSignature)
            return;
        _missionQueueSignature = signature;
        var queue = _view!.MissionQueue;
        queue.Clear();
        _activeMissionProgress = null;
        _view.MissionQueueCount.Value =
            $"{_state.MissionQueue.Count} / {database.Balance.MaximumMissionQueueSize}";
        if (_state.MissionQueue.Count == 0)
        {
            queue.Add(_document!.CreateText("Добавьте несколько поручений ниже.", new Dictionary<string, string>
            {
                ["class"] = "queue-empty"
            }));
            return;
        }

        for (var index = 0; index < _state.MissionQueue.Count; index++)
        {
            var mission = _state.MissionQueue[index];
            var config = database.GetMission(mission.MissionConfigId);
            var item = _document!.Instantiate("Components/MissionQueueItem.xml", queue,
                new Dictionary<string, string>
                {
                    ["key"] = mission.InstanceId.ToString(),
                    ["position"] = index == 0 ? "ВЫПОЛНЯЕТСЯ" : $"В ОЧЕРЕДИ · {index + 1}",
                    ["name"] = config.Name,
                    ["progress"] = index == 0
                        ? $"{Format(mission.CurrentProgress)} / {Format(mission.RequiredProgress)}"
                        : $"{Format(mission.RequiredProgress)} тактов"
                });
            var queueItem = new MissionQueueItemView(item);
            if (index == 0)
            {
                item.AddClass("active");
                _activeMissionProgress = queueItem.Progress;
            }
            var missionId = mission.InstanceId;
            var left = queueItem.MoveUp;
            var right = queueItem.MoveDown;
            left.IsEnabled = index > 0;
            right.IsEnabled = index < _state.MissionQueue.Count - 1;
            left.Clicked += _ => MoveMission(missionId, -1);
            right.Clicked += _ => MoveMission(missionId, 1);
            queueItem.Remove.Clicked += _ => RemoveMission(missionId);
        }
    }

    private void UpdateMissionQueueProgress()
    {
        if (!_view!.MissionsWindow.IsVisible)
        {
            _missionsDirty = true;
            return;
        }
        if (_activeMissionProgress is null || _state.CurrentMission is not { } mission)
            return;
        var value = $"{Format(mission.CurrentProgress)} / {Format(mission.RequiredProgress)}";
        _activeMissionProgress.Value = value;
    }

    private void UpdateMissionRefresh()
    {
        if (!_view!.MissionsWindow.IsVisible)
        {
            _missionsDirty = true;
            return;
        }
        var refreshIn = _state.Calendar.TicksPerYear - _state.Calendar.TickInYear;
        _view!.MissionRefresh.Value = $"Новые поручения через {refreshIn} такт.";
    }

    private void SyncMissionBoard()
    {
        if (!_view!.MissionsWindow.IsVisible)
        {
            _missionsDirty = true;
            return;
        }
        _missionsDirty = false;
        UpdateMissionRefresh();
        var list = _view!.MissionsList;

        var liveMissions = _state.MissionBoard.MissionIds.ToHashSet(StringComparer.Ordinal);
        foreach (var stale in _missionCards.Keys.Where(id => !liveMissions.Contains(id)).ToArray())
        {
            _missionCards[stale].Card.RemoveFromParent();
            _missionCards.Remove(stale);
        }

        if (_state.MissionBoard.MissionIds.Count == 0)
        {
            if (_missionBoardEmpty is null)
            {
                _missionBoardEmpty = _document!.CreateText("Все поручения разобраны. Новые появятся в начале следующего года.", new Dictionary<string, string>
                {
                    ["class"] = "mission-board-empty"
                });
                list.Add(_missionBoardEmpty);
            }
            return;
        }

        if (_missionBoardEmpty is not null)
        {
            _missionBoardEmpty.RemoveFromParent();
            _missionBoardEmpty = null;
        }

        foreach (var offeredMissionId in _state.MissionBoard.MissionIds)
        {
            if (_missionCards.TryGetValue(offeredMissionId, out var existing))
            {
                existing.Start.IsEnabled =
                    _state.MissionQueue.Count < database.Balance.MaximumMissionQueueSize;
                continue;
            }

            var mission = database.GetMission(offeredMissionId);
            var minimumYears = mission.MinimumDurationTicks / (decimal)_state.Calendar.TicksPerYear;
            var maximumYears = mission.MaximumDurationTicks / (decimal)_state.Calendar.TicksPerYear;
            var card = _document!.Instantiate("Components/MissionCard.xml", list, new Dictionary<string, string>
            {
                ["key"] = mission.Id,
                ["name"] = mission.Name,
                ["description"] = mission.Description,
                ["duration"] = $"{mission.MinimumDurationTicks}–{mission.MaximumDurationTicks} тактов",
                ["years"] = $"примерно {minimumYears:0.0}–{maximumYears:0.0} года"
            });
            var cardView = new MissionCardView(card);
            var rewardIcons = cardView.RewardIcons;
            var rewardItems = database.Items.Values
                .Where(item => mission.Reward.RequiredItemCategory is null ||
                               item.Category == mission.Reward.RequiredItemCategory)
                .Take(3);
            foreach (var rewardItem in rewardItems)
                AddRewardIcon(
                    rewardIcons,
                    rewardItem,
                    $"×{mission.Reward.MinimumQuantity}–{mission.Reward.MaximumQuantity}");
            if (mission.Reward.Money > 0)
                AddRewardIcon(rewardIcons, "Assets/Textures/UIIcons/money.png", $"+{mission.Reward.Money}");

            var button = cardView.Start;
            button.IsEnabled = _state.MissionQueue.Count < database.Balance.MaximumMissionQueueSize;
            var missionId = mission.Id;
            button.Clicked += _ =>
            {
                PlaySound("Sounds/ui-click.wav", 0.45f);
                var result = missions.Start(_state, missionId);
                ShowActionFeedback(
                    result.Message,
                    result.Success ? "Assets/Textures/UIIcons/missions.png" : "Assets/Textures/UIIcons/close.png",
                    result.Success);
                if (!result.Success)
                    return;
                _missionQueueSignature = null;
                UpdateMissionUi();
            };
            _missionCards.Add(offeredMissionId, cardView);
        }
    }

    private void MoveMission(Guid missionId, int offset)
    {
        PlaySound("Sounds/ui-click.wav", 0.45f);
        var result = missions.Move(_state, missionId, offset);
        ShowActionFeedback(result.Message, "Assets/Textures/UIIcons/missions.png", result.Success, info: result.Success);
        _missionQueueSignature = null;
        _activeMissionProgress = null;
        UpdateMissionUi();
    }

    private void RemoveMission(Guid missionId)
    {
        PlaySound("Sounds/ui-click.wav", 0.45f);
        var result = missions.Remove(_state, missionId);
        ShowActionFeedback(result.Message, "Assets/Textures/UIIcons/missions.png", result.Success, info: result.Success);
        _missionQueueSignature = null;
        UpdateMissionUi();
    }

    private void UpdateMissionUi()
    {
        UpdateMissionSummary();
        SyncMissionQueue();
        UpdateMissionQueueProgress();
        SyncMissionBoard();
    }

    private void ShowDeathWindow()
    {
        CloseWindows();
        var stage = database.Cultivation.Stages[_state.Character.Cultivation.StageIndex];
        _view!.DeathAge.Value = $"{_state.Character.Age.TotalYears:0.0} лет";
        _view.DeathStage.Value = $"{stage.Name} · {_state.Character.Cultivation.Level}";
        _view.DeathYear.Value = _state.Calendar.CurrentYear.ToString(CultureInfo.InvariantCulture);
        _view.DeathWindow.IsVisible = true;
    }

    private void RestartGame()
    {
        InitializeNewGame();
        _elapsedMilliseconds = 0f;
        _gameOver = false;
        _missionQueueSignature = null;
        _effectsSignature = null;
        _cultivationPathSignature = null;
        _view!.DeathWindow.IsVisible = false;
        Save();
        ApplyStateToView();
    }

    private void OpenWindow(UiPanel window)
    {
        CloseWindows();
        window.IsVisible = true;
    }

    private void OpenCultivationWindow()
    {
        OpenWindow(_view!.CultivationWindow);
        UpdateHudAndCultivation();
        if (_cultivationPathDirty)
            SyncCultivationPath();
        _centerCultivationPathPending = true;
    }

    private void OpenDataWindow(UiPanel window)
    {
        OpenWindow(window);
        if (ReferenceEquals(window, _view!.ShopWindow) && _shopDirty)
            SyncShop();
        else if (ReferenceEquals(window, _view.InventoryWindow) && _inventoryDirty)
            SyncInventory();
        else if (ReferenceEquals(window, _view.MissionsWindow) && _missionsDirty)
        {
            SyncMissionQueue();
            UpdateMissionQueueProgress();
            SyncMissionBoard();
        }
    }

    private void SyncCultivationPath()
    {
        if (!_view!.CultivationWindow.IsVisible)
        {
            _cultivationPathDirty = true;
            return;
        }
        var progress = _state.Character.Cultivation;
        var signature = $"{progress.StageIndex}:{progress.Level}";
        if (_cultivationPathSignature == signature)
            return;

        _cultivationPathSignature = signature;
        _cultivationPathDirty = false;
        _currentPathNode = null;
        var host = _view!.CultivationPath;
        host.Clear();
        for (var stageIndex = 0; stageIndex < database.Cultivation.Stages.Count; stageIndex++)
        {
            var stage = database.Cultivation.Stages[stageIndex];
            var stageState = stageIndex < progress.StageIndex
                ? " completed-stage"
                : stageIndex == progress.StageIndex
                    ? " current-stage"
                    : string.Empty;
            var stagePanel = _document!.CreateElement("panel", new Dictionary<string, string>
            {
                ["class"] = $"path-stage{stageState}"
            });
            var heading = _document.CreateElement("panel", new Dictionary<string, string>
            {
                ["class"] = "path-stage-heading"
            });
            heading.Add(_document.CreateElement("text", new Dictionary<string, string>
            {
                ["class"] = "path-stage-index"
            }, $"СТАДИЯ {stageIndex + 1}"));
            heading.Add(_document.CreateElement("text", new Dictionary<string, string>
            {
                ["class"] = "path-stage-name"
            }, stage.Name));
            stagePanel.Add(heading);
            var nodes = _document.CreateElement("panel", new Dictionary<string, string>
            {
                ["class"] = "path-nodes"
            });
            stagePanel.Add(nodes);

            for (var level = 1; level <= 10; level++)
            {
                var completed = stageIndex < progress.StageIndex ||
                                stageIndex == progress.StageIndex && level < progress.Level;
                var current = stageIndex == progress.StageIndex && level == progress.Level;

                if (level < 10)
                {
                    var connector = _document.CreateElement("panel", new Dictionary<string, string>
                    {
                        ["class"] = "path-connector"
                    });
                    if (completed)
                        connector.AddClass("completed");
                    nodes.Add(connector);
                }

                var node = _document.CreateElement("panel", new Dictionary<string, string>
                {
                    ["class"] = "path-node"
                });
                node.Add(_document.CreateElement("text", text: level.ToString(CultureInfo.InvariantCulture)));
                if (completed)
                    node.AddClass("completed");
                if (current)
                {
                    node.AddClass("current");
                    _currentPathNode = node;
                }
                nodes.Add(node);
            }
            host.Add(stagePanel);
        }

        if (_view.CultivationWindow.IsVisible)
            _centerCultivationPathPending = true;
    }

    private void TryCenterCultivationPath()
    {
        if (_currentPathNode is not { Bounds.Height: > 0 } node)
            return;
        var scroll = _view!.CultivationPath;
        if (scroll.Bounds.Height <= 0 || !scroll.CanScrollVertically)
            return;
        var target = node.Bounds.Y - scroll.Bounds.Y + node.Bounds.Height * 0.5f - scroll.Bounds.Height * 0.5f;
        scroll.ScrollTo(new Vector2(0.0f, Math.Max(0.0f, target)));
        _centerCultivationPathPending = false;
    }

    private void CloseWindows()
    {
        if (_view is null)
            return;
        foreach (var window in _view.Windows)
            window.IsVisible = false;
        CloseInfoPopup();
        CloseEffectPopup();
    }

    private void CloseInfoPopup()
    {
        if (_view is not null)
            _view.InfoPopup.IsVisible = false;
    }

    private void CloseEffectPopup()
    {
        if (_view is not null)
            _view.EffectPopup.IsVisible = false;
        _effectItemAction = null;
    }

    private void SpawnFloatingValue(decimal value, string label, string tone)
    {
        if (_view is null || value == 0m)
            return;
        var poolKey = (tone, value < 0m);
        if (!_floatingValuePools.TryGetValue(poolKey, out var pool) || pool.Count == 0)
            return;
        var sequence = _floatingValueIndices.GetValueOrDefault(poolKey);
        _floatingValueIndices[poolKey] = sequence + 1;
        var element = pool[sequence % pool.Count];
        element.Value = $"{Signed(value)} {label}";
        _floatingDocument?.RestartAnimation(element);
    }

    private void BuildFloatingUi(UiDocument document)
    {
        _floatingValuePools.Clear();
        _floatingValueIndices.Clear();
        _tapFeedback = document.GetElementById<UiPanel>("tap-feedback");
        var host = document.GetElementById<UiPanel>("tick-float-layer");
        host.Clear();
        var lane = 0;
        foreach (var tone in new[] { "spirit-value", "mission-value", "money-value" })
        {
            foreach (var negative in new[] { false, true })
            {
                var key = (tone, negative);
                var pool = new List<UiText>(2);
                _floatingValuePools.Add(key, pool);
                for (var index = 0; index < 2; index++)
                {
                    var signClass = negative ? " negative" : string.Empty;
                    var element = document.CreateText(attributes: new Dictionary<string, string>
                    {
                        ["class"] = $"tick-float {tone} lane-{lane++ % 6}{signClass}",
                        ["animation-trigger"] = "manual",
                        ["aria-hidden"] = "true"
                    });
                    host.Add(element);
                    pool.Add(element);
                }
            }
        }
    }

    private void BuildTransientUi(UiDocument document)
    {
        _actionToast = document.GetElementById<UiPanel>("action-toast");
        _actionToastIcon = document.GetElementById<UiImage>("action-toast-icon");
        _actionToastText = document.GetElementById<UiText>("action-toast-text");
        _actionToast.AnimationEnded += (sender, _) => sender.IsVisible = false;
    }

    private void ShowActionFeedback(string message, string icon, bool success, bool info = false)
    {
        if (_actionToast is null || _actionToastIcon is null || _actionToastText is null)
            return;
        var toast = _actionToast;
        toast.IsVisible = false;
        _alternateActionToast = !_alternateActionToast;
        var tone = info ? "toast-info" : success ? "toast-success" : "toast-error";
        var animation = _alternateActionToast ? "toast-a" : "toast-b";
        toast.SetAttribute("class", $"action-toast {tone} {animation}");
        _actionToastIcon.Source = icon;
        _actionToastText.Value = message;
        toast.IsVisible = true;
    }

    private void BindClick(UiButton button, Action action) =>
        button.Clicked += _ =>
        {
            PlaySound("Sounds/ui-click.wav", 0.45f);
            action();
        };

    private void PlaySound(string path, float volume)
    {
        try
        {
            audio.Play(path, volume: volume);
        }
        catch
        {
            // Audio failure must not interrupt game state changes.
        }
    }

    private static string Format(decimal value) => value.ToString("0.##", CultureInfo.InvariantCulture);
    private static string Signed(decimal value) => value >= 0m ? $"+{value:0.#}" : $"{value:0.#}";

    private void AddRewardIcon(UiElement parent, ItemConfig item, string badge)
    {
        var tile = AddRewardIcon(parent, item.Icon, badge);
        tile.Clicked += _ => ShowItemPopup(
            item,
            null,
            badge.TrimStart('×'),
            "Возможная награда · редкость и качество определятся при получении");
    }

    private UiElement AddRewardIcon(UiElement parent, string source, string badge)
    {
        var tile = _document!.CreateElement("panel", new Dictionary<string, string>
        {
            ["class"] = "reward-icon-tile"
        });
        tile.Add(_document.CreateElement("image", new Dictionary<string, string>
        {
            ["class"] = "reward-item-icon",
            ["src"] = source
        }));
        tile.Add(_document.CreateElement("text", new Dictionary<string, string>
        {
            ["class"] = "reward-icon-badge"
        }, badge));
        parent.Add(tile);
        return tile;
    }

    private void BuildQualityStars(UiElement host, decimal quality)
    {
        host.Clear();
        var stars = _document!.Instantiate("Components/QualityStars.xml", host);
        new QualityStarsView(stars).SetQuality(quality);
    }

    private void ShowItemPopup(
        ItemConfig config,
        ItemInstance? item,
        string quantity,
        string context)
    {
        PlaySound("Sounds/ui-click.wav", 0.45f);
        var rarity = item is null ? null : database.GetRarity(item.Rarity);
        var quality = item?.Quality ?? 2.5m;

        var view = _view!;
        view.InfoPopupKind.Value = $"{ItemCategoryName(config.Category)} · {DurationName(config.DurationType)}";
        view.InfoPopupTitle.Value = config.Name;
        view.InfoPopupDescription.Value = config.Description;
        view.InfoPopupEffect.Value = DescribeItemEffect(config, quality);
        view.InfoPopupStatLabel1.Value = "КОЛИЧЕСТВО";
        view.InfoPopupStatValue1.Value = quantity;
        view.InfoPopupStatLabel2.Value = "КАЧЕСТВО";
        view.InfoPopupStatLabel3.Value = "РЕДКОСТЬ";
        view.InfoPopupStatValue3.Value = rarity?.DisplayName ?? "При получении";
        view.InfoPopupDetails.Value = context;

        var qualityStars = view.InfoPopupQuality;
        var qualityFallback = view.InfoPopupStatValue2;
        qualityStars.IsVisible = item is not null;
        qualityFallback.IsVisible = item is null;
        if (item is not null)
            BuildQualityStars(qualityStars, quality);
        else
            qualityFallback.Value = "При получении";

        view.InfoPopupIcon.Source = config.Icon;
        var accent = rarity?.Color ?? "#56d5a0";
        view.InfoPopupKind.Style.Color = accent;
        view.InfoPopupCard.Style.BorderColor = accent;
        view.InfoPopupIconWell.Style.BorderColor = accent;
        view.InfoPopup.IsVisible = true;
    }

    private void ShowEffectPopup(EffectType type)
    {
        var active = _state.ActiveEffects
            .Where(effect => !effect.IsExpired && effect.Type == type)
            .ToArray();
        if (active.Length == 0)
            return;

        PlaySound("Sounds/ui-click.wav", 0.45f);
        var total = CalculateEffectTotal(type, active);
        var sourceGroups = active
            .GroupBy(effect => effect.SourceItemId)
            .Select(group => (Config: database.GetItem(group.Key), Count: group.Count(), Effect: group.First()))
            .ToArray();
        var remaining = active
            .Where(effect => !effect.IsPermanent)
            .Select(effect => effect.RemainingTicks ?? 0)
            .DefaultIfEmpty()
            .Min();
        var duration = active.All(effect => effect.IsPermanent)
            ? "Постоянно"
            : $"{remaining} тактов";
        var primary = sourceGroups[0];
        var rarity = database.GetRarity(primary.Effect.SourceRarity);
        var quality = primary.Effect.SourceQuality;
        var accent = rarity.Color;
        var sources = string.Join(", ", sourceGroups.Select(source =>
            source.Count > 1 ? $"{source.Config.Name} ×{source.Count}" : source.Config.Name));

        var view = _view!;
        view.EffectPopupKind.Value = $"{EffectName(type)} · ЭФФЕКТ АКТИВЕН";
        view.EffectPopupTitle.Value = primary.Config.Name;
        view.EffectPopupDescription.Value = primary.Config.Description;
        view.EffectPopupEffect.Value =
            $"{DescribeItemEffect(primary.Config, quality)} · Суммарно от всех стаков: {EffectValue(type, total)}";
        view.EffectPopupStacks.Value = active.Length.ToString(CultureInfo.InvariantCulture);
        view.EffectPopupRarity.Value = rarity.DisplayName;
        view.EffectPopupDetails.Value = $"Осталось: {duration} · Источники: {sources}";

        BuildQualityStars(view.EffectPopupQuality, quality);
        view.EffectPopupIcon.Source = primary.Config.Icon;
        view.EffectPopupKind.Style.Color = accent;
        view.EffectPopupCard.Style.BorderColor = accent;
        view.EffectPopupIconWell.Style.BorderColor = accent;

        var sourceItem = new ItemInstance
        {
            InstanceId = Guid.Empty,
            ConfigId = primary.Config.Id,
            Rarity = primary.Effect.SourceRarity,
            Quality = primary.Effect.SourceQuality
        };
        var sourceCount = primary.Count.ToString(CultureInfo.InvariantCulture);
        _effectItemAction = () => ShowItemPopup(
            primary.Config,
            sourceItem,
            sourceCount,
            $"Источник активного эффекта «{EffectName(type)}». В эффекте сейчас {primary.Count} шт.");
        view.EffectPopup.IsVisible = true;
    }

    private decimal CalculateEffectTotal(EffectType type, IReadOnlyList<ActiveEffect> active)
    {
        var baseValue = type == EffectType.BreakthroughChance ? 0m : 1m;
        var calculated = ModifierCalculator.Calculate(baseValue, active, type);
        return type == EffectType.BreakthroughChance
            ? calculated
            : (calculated - 1m) * 100m;
    }

    private float CalculateEffectTimer(IReadOnlyList<ActiveEffect> active)
    {
        var temporary = active.Where(effect => !effect.IsPermanent).ToArray();
        if (temporary.Length == 0)
            return 1.0f;

        return (float)temporary.Min(effect =>
        {
            var duration = Math.Max(1, database.GetItem(effect.SourceItemId).TemporaryDurationTicks);
            return Math.Clamp((effect.RemainingTicks ?? 0) / (decimal)duration, 0m, 1m);
        });
    }

    private static string EffectValue(EffectType type, decimal value) =>
        type == EffectType.BreakthroughChance
            ? $"{Signed(value)} п.п."
            : $"{Signed(value)}%";

    private static string ItemCategoryName(ItemCategory category) => category switch
    {
        ItemCategory.Pill => "ПИЛЮЛЯ",
        ItemCategory.Core => "АРТЕФАКТ",
        ItemCategory.Ingredient => "ИНГРЕДИЕНТ",
        _ => category.ToString().ToUpperInvariant()
    };

    private static string DurationName(ItemDurationType duration) => duration switch
    {
        ItemDurationType.Instant => "МГНОВЕННО",
        ItemDurationType.Temporary => "ВРЕМЕННО",
        ItemDurationType.Permanent => "ПОСТОЯННО",
        _ => duration.ToString().ToUpperInvariant()
    };

    private static string EffectName(EffectType type) => type switch
    {
        EffectType.TickEfficiency => "Эффективность",
        EffectType.AgingSpeed => "Старение",
        EffectType.BreakthroughChance => "Прорыв",
        EffectType.SpiritualPowerGain => "Духовная сила",
        EffectType.MissionProgress => "Миссии",
        _ => type.ToString()
    };

    private string DescribeItemEffect(ItemConfig config, ItemInstance item) =>
        DescribeItemEffect(config, item.Quality);

    private string DescribeItemEffect(ItemConfig config, decimal quality)
    {
        if (config.Effects.Count == 0)
            return "Материал для алхимии. Можно выгодно продать лавочнику.";

        var strength = database.Balance.EffectQualityBase +
                       quality * database.Balance.EffectQualityPerPoint;
        var descriptions = config.Effects.Select(effect => DescribeEffect(effect, strength));
        var duration = config.DurationType switch
        {
            ItemDurationType.Instant => "Срабатывает сразу.",
            ItemDurationType.Permanent => "Эффект остаётся навсегда.",
            _ => $"Действует {config.TemporaryDurationTicks} тактов."
        };
        return $"{string.Join("; ", descriptions)}. {duration}";
    }

    private static string DescribeEffect(ItemEffectDefinition effect, decimal strength)
    {
        var value = effect.Value * strength;
        return effect.Type switch
        {
            EffectType.TickEfficiency => $"Результат каждого такта {Signed(value)}%",
            EffectType.AgingSpeed => $"Скорость старения {Signed(value)}%",
            EffectType.BreakthroughChance when effect.Operation == ModifierOperation.Flat =>
                $"Шанс прорыва {Signed(value)} п.п.",
            EffectType.BreakthroughChance => $"Шанс прорыва {Signed(value)}%",
            EffectType.SpiritualPowerGain when effect.Operation == ModifierOperation.Flat =>
                $"Духовная сила {Signed(value)}",
            EffectType.SpiritualPowerGain => $"Получение духовной силы {Signed(value)}%",
            EffectType.MissionProgress => $"Скорость выполнения миссий {Signed(value)}%",
            _ => $"{EffectName(effect.Type)} {Signed(value)}%"
        };
    }
}
