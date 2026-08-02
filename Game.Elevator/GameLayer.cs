using Autofac;
using Vecxy.Assets;
using Game.Elevator.InteractiveMap;
using JetBrains.Annotations;
using Vecxy.Diagnostics;
using Vecxy.Engine;
using Vecxy.Kernel;
using Vecxy.Scene;
using Vecxy.UI;

namespace Game.Elevator;

[UsedImplicitly]
public class GameLayer
(
    ISceneManager scenes,
    IWindow window,
    IUiManager ui
) : AAppLayer
{
    private SceneInstance? _mainScene;
    private SceneInstance? _gameplayScene;
    private IInteractiveMap? _map;
    private int? _pendingRegionId;
    private bool _toggleMapRequested;
    private bool _mapOpen;
    private bool _mKeyDown;
    private UiDocument? _uiDocument;

    public class Definition : ADefinition<GameLayer>
    {
        public override void RegisterGlobal(ContainerBuilder builder)
        {
            builder
                .RegisterType<GameScene>()
                .AsSelf();

            builder.RegisterType<ApartmentScene>().AsSelf();
            builder.RegisterType<BusStationScene>().AsSelf();
            builder.RegisterType<FactoryScene>().AsSelf();
            builder.RegisterType<HouseScene>().AsSelf();
            builder.RegisterType<LakeScene>().AsSelf();
            builder.RegisterType<BridgeScene>().AsSelf();
            builder.RegisterType<ParkScene>().AsSelf();
            builder.RegisterType<WarehouseScene>().AsSelf();
            builder.RegisterType<TowerScene>().AsSelf();

        }
    }
    
    public override void OnInitialize()
    {
        base.OnInitialize();

        window.SetCursorCaptured(false);
        window.KeyChanged += OnKeyChanged;

        _mainScene = scenes.LoadScene<GameScene>();
        _map = ((GameScene)_mainScene.Scene).Map ??
            throw new InvalidOperationException("Interactive map was not created.");
        _map.RegionClicked += OnRegionClicked;
        _map.IsVisible = false;

        // The region is loaded in addition to the persistent main scene.
        _gameplayScene = scenes.LoadSceneAdditive<ApartmentScene>();
        scenes.SetActiveScene(_gameplayScene);
        Logger.Info("Initial region scene loaded: 1 / Apartment. Press M to open the map.");

        _uiDocument = ui.Load("UI/window.xml");
        _uiDocument.Reloaded += BindUiCallbacks;
        BindUiCallbacks(_uiDocument);
    }

    public override void OnUpdate(float deltaTime)
    {
#if ANDROID
        if (MobileInput.ConsumeMapToggle())
            _toggleMapRequested = true;
#endif

        if (_pendingRegionId is { } regionId)
        {
            _pendingRegionId = null;
            _toggleMapRequested = false;
            SwitchToRegion(regionId);
            return;
        }

        if (!_toggleMapRequested)
            return;

        _toggleMapRequested = false;

        if (_mapOpen)
            CloseMap();
        else
            OpenMap();
    }

    public override void OnUnload()
    {
        window.KeyChanged -= OnKeyChanged;

        if (_uiDocument is not null)
        {
            _uiDocument.Reloaded -= BindUiCallbacks;
            ui.Unload(_uiDocument);
            _uiDocument = null;
        }

        if (_map is not null)
            _map.RegionClicked -= OnRegionClicked;

        if (_gameplayScene is not null && scenes.LoadedScenes.Contains(_gameplayScene))
            scenes.UnloadScene(_gameplayScene);

        if (_mainScene is not null && scenes.LoadedScenes.Contains(_mainScene))
            scenes.UnloadScene(_mainScene);

        _mainScene = null;
        _gameplayScene = null;
        _map = null;
        _mapOpen = false;
    }

    private void BindUiCallbacks(UiDocument document)
    {
        if (document.Query("#toggle-map") is { } toggleMap)
            toggleMap.Clicked += OnToggleMapClicked;
        if (document.Query("#close-ui") is { } closeUi)
            closeUi.Clicked += OnCloseUiClicked;
    }

    private void OnKeyChanged(IWindow.KeyEvent eventData)
    {
        if (eventData.Key != (int)EKeyboardKey.M)
            return;

        if (!eventData.IsPressed)
        {
            _mKeyDown = false;
            return;
        }

        if (_mKeyDown)
            return;

        _mKeyDown = true;
        _toggleMapRequested = true;
    }

    private void OnToggleMapClicked(UiElement _)
    {
        _toggleMapRequested = true;
    }

    private void OnCloseUiClicked(UiElement _)
    {
        if (_uiDocument is not null)
            _uiDocument.IsVisible = false;
    }

    private void OpenMap()
    {
        if (_mainScene is null || _map is null)
            throw new InvalidOperationException("Persistent main scene is not loaded.");

        scenes.SetActiveScene(_mainScene);
        _map.IsVisible = true;
        _mapOpen = true;
        window.SetCursorCaptured(false);
        Logger.Info("Interactive map enabled from the persistent main scene.");
    }

    private void CloseMap()
    {
        if (_map is not null)
            _map.IsVisible = false;

        _mapOpen = false;

        if (_gameplayScene is not null && scenes.LoadedScenes.Contains(_gameplayScene))
            scenes.SetActiveScene(_gameplayScene);

        Logger.Info("Interactive map disabled; additive region scene restored.");
    }

    private void OnRegionClicked(MapRegion region)
    {
        _pendingRegionId = region.Id;
    }

    private void SwitchToRegion(int regionId)
    {
        if (_map is not null)
            _map.IsVisible = false;

        _mapOpen = false;

        if (_gameplayScene is not null && scenes.LoadedScenes.Contains(_gameplayScene))
            scenes.UnloadScene(_gameplayScene);

        _gameplayScene = regionId switch
        {
            1 => scenes.LoadSceneAdditive<ApartmentScene>(),
            2 => scenes.LoadSceneAdditive<BusStationScene>(),
            3 => scenes.LoadSceneAdditive<FactoryScene>(),
            4 => scenes.LoadSceneAdditive<HouseScene>(),
            5 => scenes.LoadSceneAdditive<LakeScene>(),
            6 => scenes.LoadSceneAdditive<BridgeScene>(),
            7 => scenes.LoadSceneAdditive<ParkScene>(),
            8 => scenes.LoadSceneAdditive<WarehouseScene>(),
            9 => scenes.LoadSceneAdditive<TowerScene>(),
            _ => throw new ArgumentOutOfRangeException(
                nameof(regionId),
                regionId,
                "Unknown interactive map region.")
        };

        scenes.SetActiveScene(_gameplayScene);
        window.SetCursorCaptured(false);
        Logger.Info($"Region scene switched: {regionId} / {_gameplayScene.Scene.GetType().Name}.");
    }
}
