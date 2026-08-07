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
    private UiPanel? _windowLayer;
    private UiPanel? _actionToast;
    private UiImage? _actionToastIcon;
    private UiText? _actionToastText;
    private float _actionToastRemaining;
    private UiPanel? _tapFeedback;
    private UiPanel? _achievementEffect;
    private UiText? _achievementText;
    private GameState _state = null!;
    private float _elapsedMilliseconds;
    private bool _gameOver;
    private bool _alternateActionToast;
    private ItemCategory _inventoryCategory = ItemCategory.Ingredient;
    private Guid? _selectedInventoryItem;
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
        if (_actionToast is not null && _actionToast.IsVisible)
        {
            _actionToastRemaining -= deltaTime;
            if (_actionToastRemaining <= 0f)
                _actionToast.IsVisible = false;
        }

        if (_gameOver)
            return;
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
    }

    private void InitializeNewGame()
    {
        _state = new GameState(database.Balance.TicksPerYear);
        _state.Character.Restore(0m, 0, database.Balance.StartingAgeYears);
        _state.Character.AddMoney(database.Balance.StartingMoney);
        _state.SetActivityMode(ActivityMode.Cultivation);
        shop.Refresh(_state.Shop);
        missions.Refresh(_state);
    }

    private void BuildUi(UiDocument document)
    {
        var layer = document.GetElementById<UiPanel>("window-layer");
        _windowLayer = layer;
        layer.Clear();
        document.Instantiate("Components/ShopWindow.xml", layer);
        document.Instantiate("Components/InventoryWindow.xml", layer);
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

        BindClick(_view.ShopButton, () => { OpenWindow(_view.ShopWindow); SyncShop(); });
        BindClick(_view.InventoryButton, () => { OpenWindow(_view.InventoryWindow); SyncInventory(); });
        BindClick(_view.MissionsButton, OpenMissions);
        BindClick(_view.MissionSummaryButton, OpenMissions);
        BindClick(_view.CultivateMode, () => SetActivity(ActivityMode.Cultivation));
        BindClick(_view.MissionsMode, () => SetActivity(ActivityMode.Missions));
        BindClick(_view.Breakthrough, OpenBreakthrough);
        BindClick(_view.ConfirmBreakthrough, AttemptBreakthrough);
        BindClick(_view.CancelBreakthrough, () => UnmountWindow(_view.BreakthroughWindow));
        BindClick(_view.BreakthroughResultOk, () => UnmountWindow(_view.BreakthroughResult));
        BindClick(_view.Restart, RestartGame);
        BindClick(_view.InfoPopupOk, CloseInfoPopup);
        BindClick(_view.InfoPopupClose, CloseInfoPopup);
        BindClick(_view.EffectPopupClose, CloseEffectPopup);
        _view.EffectPopup.Clicked += _ => CloseEffectPopup();
        _view.CharacterTapTarget.ClickedAt += (_, position) => TapCharacter(position);
        BindClick(_view.AvailableMissionsTab, () => ShowMissionPage(false));
        BindClick(_view.AcceptedMissionsTab, () => ShowMissionPage(true));
        BindClick(_view.IngredientsTab, () => SelectInventoryCategory(ItemCategory.Ingredient));
        BindClick(_view.CoresTab, () => SelectInventoryCategory(ItemCategory.Core));
        BindClick(_view.PillsTab, () => SelectInventoryCategory(ItemCategory.Pill));
        BindClick(_view.InventoryUse, UseSelectedItem);
        BindClick(_view.InventorySell, SellSelectedItem);
        foreach (var close in _view.WindowCloseButtons)
            close.Clicked += _ => CloseWindows();

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
            _tapFeedback.SetStyle("left", $"{position.X:0}px");
            _tapFeedback.SetStyle("top", $"{position.Y:0}px");
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

    private void SetActivity(ActivityMode mode)
    {
        _state.SetActivityMode(mode);
        UpdateActivityButtons();
        Save();
    }

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
        var fraction = required <= 0m ? 1m : Math.Clamp(character.SpiritualPower / required, 0m, 1m);
        _view!.StageName.Value = stage.Name;
        _view.YearDial.Progress = 1f - _state.Calendar.TickInYear / (float)_state.Calendar.TicksPerYear;
        _view.Money.Value = character.Money.ToString("N0", CultureInfo.InvariantCulture);
        _view.Age.Value = $"{character.Age.TotalYears:0.0} / {cultivation.GetMaximumAge(character):0}";
        _view.Realm.Value = $"{stage.Name} · ур. {progress.Level}";
        _view.CultivationProgressText.Value = $"{Format(character.SpiritualPower)} / {Format(required)}";
        _view.CultivationProgress.Progress = (float)fraction;
        _view.Breakthrough.IsEnabled = progress.CanAttemptBreakthrough &&
                                      progress.StageIndex < database.Cultivation.Stages.Count - 1 &&
                                      character.SpiritualPower >= required;
        UpdateActivityButtons();
    }

    private void UpdateActivityButtons()
    {
        if (_view is null)
            return;
        _view.CultivateMode.ToggleClass("active", _state.ActivityMode == ActivityMode.Cultivation);
        _view.MissionsMode.ToggleClass("active", _state.ActivityMode == ActivityMode.Missions);
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
            return;
        }
        var config = database.GetMission(mission.MissionConfigId);
        _view!.MissionName.Value = config.Name;
        _view.MissionDescription.Value = _state.ActivityMode == ActivityMode.Missions
            ? "Выполняется сейчас"
            : "Ожидает: включите режим миссий";
        _view.MissionProgressText.Value = $"{Format(mission.CurrentProgress)} / {Format(mission.RequiredProgress)}";
        _view.MissionProgress.Progress = (float)(mission.RequiredProgress == 0m ? 1m : mission.CurrentProgress / mission.RequiredProgress);
    }

    private void SyncEffects()
    {
        var groups = _state.ActiveEffects.Where(effect => !effect.IsExpired)
            .GroupBy(effect => effect.Type).OrderBy(group => group.Key).ToArray();
        var signature = string.Join('|', groups.SelectMany(group => group.Select(effect =>
            $"{effect.Type}:{effect.SourceItemId}:{effect.Value}:{effect.DurationType}")));
        var currentSignature = _view!.Effects.Attributes.GetValueOrDefault("data-signature");
        if (signature != currentSignature)
        {
            _view.Effects.SetAttribute("data-signature", signature);
            _view.Effects.Clear();
            _effectWidgets.Clear();
            if (groups.Length == 0)
            {
                _view.Effects.Add(_document!.CreateText("Нет активных эффектов", new Dictionary<string, string> { ["class"] = "detail-text" }));
                return;
            }
            foreach (var group in groups)
            {
                var source = database.GetItem(group.First().SourceItemId);
                var orb = _document!.CreateButton(attributes: new Dictionary<string, string> { ["class"] = "effect-orb" });
                var ring = (UiRadialProgress)_document.CreateElement("radial-progress", new Dictionary<string, string> { ["class"] = "effect-ring" });
                orb.Add(ring);
                orb.Add(_document.CreateImage(source.Icon, new Dictionary<string, string> { ["class"] = "effect-icon" }));
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
        _view.ShopMarkup.Value = $"Наценка: +{_state.Shop.BuyMarkupPercent}%";
        _view.ShopMoney.Value = $"{_state.Character.Money:N0} рублей";
        _shopCards.Update(_state.Shop.Slots, slot => slot.SlotId);
    }

    private ShopCardView CreateShopCard(ShopSlot slot)
    {
        var root = _document!.Instantiate("Components/ShopCard.xml", _view!.ShopGrid, new Dictionary<string, string>
        {
            ["key"] = slot.SlotId.ToString(), ["icon"] = string.Empty, ["name"] = string.Empty,
            ["meta"] = string.Empty, ["effect"] = string.Empty, ["price"] = string.Empty
        });
        var card = new ShopCardView(root);
        var slotId = slot.SlotId;
        card.IconWell.Clicked += _ => ShowShopItem(slotId);
        card.Buy.Clicked += _ => BuyShopItem(slotId);
        return card;
    }

    private void UpdateShopCard(ShopCardView card, ShopSlot slot, int _)
    {
        var config = database.GetItem(slot.Item.ConfigId);
        var rarity = database.GetRarity(slot.Item.Rarity);
        var unitPrice = prices.GetBuyPrice(slot.Item, _state.Shop);
        card.Icon.Source = config.Icon;
        card.Name.Value = config.Name;
        card.Meta.Value = $"В наличии: {slot.AvailableQuantity}";
        card.Effect.Value = DescribeItemEffect(config, slot.Item);
        card.Buy.Label = $"КУПИТЬ · {unitPrice.ToString(CultureInfo.InvariantCulture)}";
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
        ShowItemPopup(config, slot.Item, slot.AvailableQuantity.ToString(),
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
        _view.SellRate.Value = $"Продажа: {_state.Shop.SellAdjustmentPercent}%";
        _view.IngredientsTab.ToggleClass("active", _inventoryCategory == ItemCategory.Ingredient);
        _view.CoresTab.ToggleClass("active", _inventoryCategory == ItemCategory.Core);
        _view.PillsTab.ToggleClass("active", _inventoryCategory == ItemCategory.Pill);
        _inventoryIcons.Update(
            _state.Inventory.Items.Where(item => database.GetItem(item.ConfigId).Category == _inventoryCategory),
            item => item.InstanceId);
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
        var id = item.InstanceId;
        root.Clicked += _ => SelectInventoryItem(id);
        return icon;
    }

    private void UpdateInventoryIcon(InventoryIconView icon, ItemInstance item, int _)
    {
        var config = database.GetItem(item.ConfigId);
        icon.Icon.Source = config.Icon;
        icon.Quantity.Value = $"×{item.Quantity}";
        icon.IconWell.Style.BorderColor = database.GetRarity(item.Rarity).Color;
    }

    private void SelectInventoryItem(Guid id)
    {
        var item = _state.Inventory.Find(id);
        if (item is null)
            return;
        _selectedInventoryItem = id;
        var config = database.GetItem(item.ConfigId);
        var rarity = database.GetRarity(item.Rarity);
        _view!.InventoryDetailIcon.Source = config.Icon;
        _view.InventoryDetailIconWell.Style.BorderColor = rarity.Color;
        _view.InventoryDetailName.Value = $"{config.Name} · ×{item.Quantity}";
        _view.InventoryDetailEffect.Value = DescribeItemEffect(config, item);
        _view.InventoryUse.IsEnabled = config.Effects.Count > 0;
        _view.InventorySell.Label = $"ПРОДАТЬ · {prices.GetSellPrice(item, _state.Shop)}";
        _view.InventoryDetails.IsVisible = true;
    }

    private void UseSelectedItem()
    {
        if (_selectedInventoryItem is not { } id || _state.Inventory.Find(id) is not { } item)
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
            _selectedInventoryItem = _state.Inventory.Find(id) is null ? null : id;
            ApplyStateToView();
            SyncInventory();
        }
    }

    private void SellSelectedItem()
    {
        if (_selectedInventoryItem is not { } id || _state.Inventory.Find(id) is not { } item)
            return;
        var config = database.GetItem(item.ConfigId);
        var result = transactions.Sell(_state, id);
        ShowActionFeedback(result.Success ? $"Продано: {config.Name} · +{result.TotalPrice:N0} руб." : result.Message,
            result.Success ? "Assets/Textures/UIIcons/money.png" : "Assets/Textures/UIIcons/close.png", result.Success);
        if (result.Success)
        {
            SpawnFloatingValue(result.TotalPrice, "РУБ.", "money-value");
            _selectedInventoryItem = _state.Inventory.Find(id) is null ? null : id;
            UpdateHud();
            SyncInventory();
            SyncShop();
        }
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
        var possible = database.Items.Values
            .Where(item => mission.Reward.RequiredItemCategory is null || item.Category == mission.Reward.RequiredItemCategory)
            .Take(1);
        foreach (var item in possible)
            AddRewardIcon(card.RewardIcons, item, item.Category == ItemCategory.Ingredient ? "×1–15" : "×1–3");
        if (mission.Reward.Money > 0)
            AddRewardIcon(card.RewardIcons, "Assets/Textures/UIIcons/money.png", $"{mission.Reward.Money}");
        card.Start.Clicked += _ => StartMission(missionId);
        return card;
    }

    private void UpdateMissionCard(MissionCardView card, string missionId, int _)
    {
        var mission = database.GetMission(missionId);
        card.Name.Value = mission.Name;
        card.Description.Value = mission.Description;
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
        card.MoveUp.IsEnabled = index > 0;
        card.MoveDown.IsEnabled = index < _state.MissionQueue.Count - 1;
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
        _view!.BreakthroughChance.Value = $"{cultivation.GetBreakthroughChance(_state.Character, _state.ActiveEffects):0.#}%";
        _view.BreakthroughCost.Value = $"Стоимость: {Format(required)} духовной силы";
        OpenWindow(_view.BreakthroughWindow);
    }

    private void AttemptBreakthrough()
    {
        UnmountWindow(_view!.BreakthroughWindow);
        var result = cultivation.AttemptBreakthrough(_state.Character, _state.ActiveEffects);
        _view.BreakthroughResultTitle.Value = result.Success ? "ПРОРЫВ УСПЕШЕН" : "ПРОРЫВ НЕ УДАЛСЯ";
        _view.BreakthroughResultText.Value = result.Success
            ? "Вы перешли на новую ступень культивации."
            : $"Прорыв не удался, вы получили травму и потеряли {result.LevelsLost} уровней";
        OpenWindow(_view.BreakthroughResult);
        if (result.Success)
        {
            PlaySound("Sounds/breakthrough.wav", 0.7f);
            ShowAchievement("УСПЕШНЫЙ ПРОРЫВ");
            ShowActionFeedback($"Предел жизни увеличен до {cultivation.GetMaximumAge(_state.Character):0} лет.",
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
        var source = database.GetItem(active[0].SourceItemId);
        var duration = active.Any(effect => effect.IsUntilBreakthroughAttempt)
            ? "к следующей попытке прорыва"
            : active.All(effect => effect.IsPermanent)
                ? string.Empty
                : $"на {FormatDuration(active.Where(effect => !effect.IsPermanent).Min(effect => Math.Max(0, effect.RemainingTicks ?? 0)))}";
        _view!.EffectPopupEffect.Value = $"{DescribeItemEffect(source, active[0].SourceQuality)}{(string.IsNullOrEmpty(duration) ? string.Empty : $" · {duration}")}";
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
    }

    private static void UnmountWindow(UiPanel window)
    {
        window.IsVisible = false;
        window.DetachFromParent();
    }

    private void CloseWindows()
    {
        if (_view is null)
            return;
        foreach (var window in _view.Windows)
            UnmountWindow(window);
        _openEffectType = null;
    }

    private void CloseInfoPopup()
    {
        if (_view is not null)
            UnmountWindow(_view.InfoPopup);
    }

    private void ShowDeathWindow()
    {
        CloseWindows();
        var stage = database.Cultivation.Stages[_state.Character.Cultivation.StageIndex];
        _view!.DeathAge.Value = $"{_state.Character.Age.TotalYears:0.0} / {cultivation.GetMaximumAge(_state.Character):0} лет";
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
        foreach (var tone in new[] { "spirit-value", "mission-value", "money-value" })
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
    }

    private void ShowActionFeedback(string message, string icon, bool success, bool info = false)
    {
        if (_actionToast is null || _actionToastIcon is null || _actionToastText is null)
            return;
        _actionToast.IsVisible = false;
        _alternateActionToast = !_alternateActionToast;
        _actionToast.SetAttribute("class", $"action-toast {(info ? "toast-info" : success ? "toast-success" : "toast-error")} {(_alternateActionToast ? "toast-a" : "toast-b")}");
        _actionToastIcon.Source = icon;
        _actionToastText.Value = message;
        _actionToastRemaining = 1.85f;
        _actionToast.IsVisible = true;
        _transientDocument?.RestartAnimation(_actionToast);
    }

    private void ShowItemPopup(ItemConfig config, ItemInstance? item, string quantity, string context)
    {
        var rarity = item is null ? null : database.GetRarity(item.Rarity);
        var quality = item?.Quality ?? 2.5m;
        var view = _view!;
        view.InfoPopupKind.Value = ItemCategoryName(config.Category);
        view.InfoPopupTitle.Value = config.Name;
        view.InfoPopupDescription.Value = config.Description;
        view.InfoPopupEffect.Value = DescribeItemEffect(config, quality);
        view.InfoPopupStatLabel1.Value = "КОЛИЧЕСТВО";
        view.InfoPopupStatValue1.Value = quantity;
        view.InfoPopupStatLabel2.Value = "КАЧЕСТВО";
        view.InfoPopupStatLabel3.Value = "РЕДКОСТЬ";
        view.InfoPopupStatValue3.Value = rarity?.DisplayName ?? "Определится при получении";
        view.InfoPopupDetails.Value = context;
        view.InfoPopupQuality.IsVisible = item is not null;
        view.InfoPopupStatValue2.IsVisible = item is null;
        if (item is not null)
            BuildQualityStars(view.InfoPopupQuality, quality);
        else
            view.InfoPopupStatValue2.Value = "—";
        view.InfoPopupIcon.Source = config.Icon;
        var accent = rarity?.Color ?? "#56d5a0";
        view.InfoPopupKind.Style.Color = accent;
        view.InfoPopupIconWell.Style.BorderColor = accent;
        MountWindow(view.InfoPopup, exclusive: false);
    }

    private void AddRewardIcon(UiElement parent, ItemConfig item, string badge)
    {
        var tile = AddRewardIcon(parent, item.Icon, badge);
        tile.Clicked += _ => ShowItemPopup(item, null, badge.TrimStart('×'), "Возможная награда за миссию");
    }

    private UiElement AddRewardIcon(UiElement parent, string source, string badge)
    {
        var tile = _document!.CreateElement("panel", new Dictionary<string, string> { ["class"] = "reward-icon-tile" });
        tile.Add(_document.CreateElement("image", new Dictionary<string, string> { ["class"] = "reward-item-icon", ["src"] = source }));
        tile.Add(_document.CreateElement("text", new Dictionary<string, string> { ["class"] = "reward-icon-badge" }, badge));
        parent.Add(tile);
        return tile;
    }

    private void BuildQualityStars(UiElement host, decimal quality)
    {
        host.Clear();
        var stars = _document!.Instantiate("Components/QualityStars.xml", host);
        new QualityStarsView(stars).SetQuality(quality);
    }

    private string DescribeItemEffect(ItemConfig config, ItemInstance item) => DescribeItemEffect(config, item.Quality);

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
            EffectType.TickEfficiency => $"Получение духовной силы {Signed(value)}%",
            EffectType.AgingSpeed => $"Скорость старения {Signed(value)}%",
            EffectType.BreakthroughChance when pluralBreakthroughChance => $"Шансы прорыва {Signed(value)}%",
            EffectType.BreakthroughChance => $"Шанс прорыва {Signed(value)}%",
            EffectType.SpiritualPowerGain when effect.Operation == ModifierOperation.Flat => $"Добавляет {Format(value)} духовной силы за тик и тап",
            EffectType.SpiritualPowerGain => $"Получение духовной силы {Signed(value)}%",
            EffectType.MissionProgress => $"Скорость выполнения миссий {Signed(value)}%",
            _ => $"Эффект {Signed(value)}%"
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
        EffectType.MissionProgress => "Выполнение миссий", _ => type.ToString()
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

    private static string Format(decimal value) => value.ToString("0.##", CultureInfo.InvariantCulture);
    private static string Signed(decimal value) => value >= 0m ? $"+{value:0.#}" : $"{value:0.#}";
}
