using Autofac;
using HardCore.Cultivation.Inventory;
using Vecxy.Assets;
using Vecxy.Diagnostics;
using Vecxy.Engine;
using Vecxy.Kernel;
using Vecxy.Scene;
using Vecxy.UI;

namespace HardCore.Cultivation;

public class GameLayer
(
    ISceneManager scenes,
    IWindow window,
    IUiManager ui,
    IConfigProvider configs,
    CultivationInteraction cultivationInteraction
) : AAppLayer
{
    private const int InventoryPageSize = 16;
    private UiDocument? _document;
    private UiDocument? _inventoryDocument;
    private ConfigRef<ItemCatalogConfig>? _itemCatalog;
    private ConfigRef<InventoryConfig>? _inventoryConfig;
    private InventoryState? _inventory;
    private int _itemCatalogVersion;
    private int _inventoryConfigVersion;
    private int _inventoryPage;
    private float _qi = 22.3f;
    private int _stones = 240;

    public class Definition : ADefinition<GameLayer>
    {
        public override void RegisterGlobal(ContainerBuilder builder)
        {
            RegisterScenes(builder);
            builder
                .RegisterType<CultivationInteraction>()
                .AsSelf()
                .SingleInstance();
        }

        private static void RegisterScenes(ContainerBuilder builder)
        {
            Register<MenuScene>();

            return;

            void Register<TScene>() where TScene : IScene
            {
                builder.RegisterType<TScene>().AsSelf();
            }
        }
    }

    public override void OnInitialize()
    {
        cultivationInteraction.CharacterClicked += Cultivate;
        window.SetCursorCaptured(false);
        scenes.LoadScene<MenuScene>();
        configs.Register<ItemCatalogConfig>();
        configs.Register<InventoryConfig>();
        _itemCatalog = configs.LoadConfig<ItemCatalogConfig>("Configs/Items.yaml");
        _inventoryConfig = configs.LoadConfig<InventoryConfig>("Configs/Inventory.yaml");
        ReloadInventory();

        _document = ui.Load("UI/cultivation.xml");
        _document.Reloaded += BindUi;
        BindUi(_document);

        _inventoryDocument = ui.Load("UI/inventory.xml");
        _inventoryDocument.IsVisible = false;
        _inventoryDocument.Reloaded += BindInventoryUi;
        BindInventoryUi(_inventoryDocument);
    }

    public override void OnUpdate(float deltaTime)
    {
        if (_itemCatalog is null || _inventoryConfig is null ||
            _itemCatalog.Version == _itemCatalogVersion &&
            _inventoryConfig.Version == _inventoryConfigVersion)
            return;

        try
        {
            ReloadInventory();
        }
        catch (Exception exception)
        {
            Logger.Error(exception, "Could not reload inventory configuration.");
        }
    }

    public override void OnUnload()
    {
        cultivationInteraction.CharacterClicked -= Cultivate;
        if (_inventoryDocument is not null)
        {
            _inventoryDocument.Reloaded -= BindInventoryUi;
            ui.Unload(_inventoryDocument);
            _inventoryDocument = null;
        }
        if (_document is not null)
        {
            _document.Reloaded -= BindUi;
            ui.Unload(_document);
            _document = null;
        }
        _inventoryConfig?.Dispose();
        _itemCatalog?.Dispose();
        _inventoryConfig = null;
        _itemCatalog = null;
        _inventory = null;
    }

    private void BindUi(UiDocument document)
    {
        Bind(document, "#profile", () => Show("SOUL AND REALM"));
        Bind(document, "#sect", () => Show("SECT HALL"));
        Bind(document, "#body", () => Show("BODY REFINING"));
        Bind(document, "#manual", () => Show("CULTIVATION MANUAL"));
        Bind(document, "#inventory", OpenInventory);
        Bind(document, "#treasure", () => Show("TREASURE PAVILION"));
        Bind(document, "#beast", () => Show("SPIRIT BEAST"));
        Bind(document, "#skill", () => Show("SECRET SKILLS"));
        UpdateResources();
    }

    private void BindInventoryUi(UiDocument document)
    {
        Bind(document, "#inventory-close", CloseInventory);
        Bind(document, "#item-detail-close", HideItemDetails);
        Bind(document, "#item-sell", SellSelectedItem);
        Bind(document, "#inventory-sort", SortInventory);
        Bind(document, "#inventory-previous", () => ChangeInventoryPage(-1));
        Bind(document, "#inventory-next", () => ChangeInventoryPage(1));
        for (var index = 0; index < InventoryPageSize; index++)
        {
            var slot = index;
            Bind(document, $"#slot-{index}", () => SelectInventorySlot(slot));
        }
        RefreshInventoryUi();
    }

    private static void Bind(UiDocument document, string selector, Action action)
    {
        if (document.Query(selector) is { } element)
            element.Clicked += _ => action();
    }

    private void Cultivate()
    {
        _qi = Math.Min(30.0f, _qi + 0.1f);
        if (_qi >= 30.0f)
        {
            _qi = 0.0f;
            _stones += 10;
            Show("MINOR BREAKTHROUGH +10 STONES");
        }
        else
        {
            Show("CULTIVATION +0.1 QI");
        }

        UpdateResources();
    }

    private void ReloadInventory()
    {
        if (_itemCatalog is null || _inventoryConfig is null)
            return;
        _inventory = new InventoryState(_itemCatalog.Value, _inventoryConfig.Value);
        _itemCatalogVersion = _itemCatalog.Version;
        _inventoryConfigVersion = _inventoryConfig.Version;
        _inventoryPage = 0;
        HideItemDetails();
        RefreshInventoryUi();
    }

    private void OpenInventory()
    {
        if (_inventoryDocument is null)
            return;
        _inventoryDocument.IsVisible = true;
        _inventoryPage = 0;
        HideItemDetails();
        RefreshInventoryUi();
        Show("SPIRIT BAG OPENED");
    }

    private void CloseInventory()
    {
        if (_inventoryDocument is not null)
            _inventoryDocument.IsVisible = false;
        HideItemDetails();
    }

    private void RefreshInventoryUi()
    {
        if (_inventoryDocument is null || _inventory is null)
            return;

        var pageCount = Math.Max(1, (_inventory.Stacks.Count + InventoryPageSize - 1) / InventoryPageSize);
        _inventoryPage = Math.Clamp(_inventoryPage, 0, pageCount - 1);
        SetText(_inventoryDocument, "#inventory-capacity", $"{_inventory.UsedCapacity} / {_inventory.Capacity}");
        SetText(_inventoryDocument, "#inventory-page", $"{_inventoryPage + 1} / {pageCount}");

        for (var localIndex = 0; localIndex < InventoryPageSize; localIndex++)
        {
            var stackIndex = _inventoryPage * InventoryPageSize + localIndex;
            var stack = stackIndex < _inventory.Stacks.Count
                ? _inventory.Stacks[stackIndex]
                : null;
            var button = _inventoryDocument.Query($"#slot-{localIndex}");
            var icon = _inventoryDocument.Query($"#slot-{localIndex}-icon");
            var quantity = _inventoryDocument.Query($"#slot-{localIndex}-quantity");
            if (stack is null)
            {
                button?.SetAttribute("style", "pointer-events: none");
                icon?.RemoveAttribute("sprite");
                icon?.SetAttribute("style", "visibility: hidden");
                if (quantity is not null)
                    quantity.Text = string.Empty;
                continue;
            }

            button?.SetAttribute("style", "pointer-events: auto");
            icon?.SetAttribute("sprite", stack.Item.Icon);
            icon?.SetAttribute("style", "visibility: visible");
            if (quantity is not null)
                quantity.Text = stack.Quantity.ToString();
        }
    }

    private void SelectInventorySlot(int localIndex)
    {
        if (_inventory is null || _inventoryDocument is null)
            return;
        var index = _inventoryPage * InventoryPageSize + localIndex;
        if (!_inventory.Select(index) || _inventory.Selected is not { } stack)
            return;

        _inventoryDocument.Query("#item-detail")?.SetAttribute("style", "display: flex");
        _inventoryDocument.Query("#item-detail-icon")?.SetAttribute("sprite", stack.Item.Icon);
        SetText(_inventoryDocument, "#item-detail-name", stack.Item.Name);
        SetText(_inventoryDocument, "#item-detail-quality", stack.Item.Quality.ToUpperInvariant());
        SetText(_inventoryDocument, "#item-detail-description", stack.Item.Description);
        SetText(_inventoryDocument, "#item-detail-price", stack.Item.SellPrice.ToString());
    }

    private void HideItemDetails()
    {
        _inventory?.ClearSelection();
        _inventoryDocument?.Query("#item-detail")?.SetAttribute("style", "display: none");
    }

    private void SellSelectedItem()
    {
        if (_inventory?.Selected is not { } selected)
            return;
        var name = selected.Item.Name;
        var earned = _inventory.SellSelected();
        _stones += earned;
        UpdateResources();
        RefreshInventoryUi();
        HideItemDetails();
        Show($"SOLD {name} +{earned} STONES");
    }

    private void SortInventory()
    {
        _inventory?.Sort();
        _inventoryPage = 0;
        HideItemDetails();
        RefreshInventoryUi();
    }

    private void ChangeInventoryPage(int direction)
    {
        if (_inventory is null)
            return;
        var pageCount = Math.Max(1, (_inventory.Stacks.Count + InventoryPageSize - 1) / InventoryPageSize);
        _inventoryPage = Math.Clamp(_inventoryPage + direction, 0, pageCount - 1);
        HideItemDetails();
        RefreshInventoryUi();
    }

    private static void SetText(UiDocument document, string selector, string value)
    {
        if (document.Query(selector) is { } element)
            element.Text = value;
    }

    private void UpdateResources()
    {
        if (_document?.Query("#qi-value") is { } qi)
            qi.Text = $"{_qi:0.0} / 30 QI";
        if (_document?.Query("#spirit-stones") is { } stones)
            stones.Text = _stones.ToString();
    }

    private void Show(string message)
    {
        if (_document?.Query("#toast") is { } toast)
            toast.Text = message;
    }
}
