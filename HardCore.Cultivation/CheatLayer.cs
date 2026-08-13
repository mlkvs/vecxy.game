using Autofac;
using HardCore.Cultivation.Game.Cheats;
using JetBrains.Annotations;
using Vecxy.Engine;
using Vecxy.UI;

namespace HardCore.Cultivation;

[UsedImplicitly]
public sealed class CheatLayer(
    IUiManager ui,
    CheatActionRegistry registry) : AAppLayer
{
    public sealed class Definition : ADefinition<CheatLayer>
    {
        public override void RegisterLocal(ContainerBuilder builder)
        {
            builder.RegisterType<CheatActionRegistry>().SingleInstance();
            builder.RegisterType<CultivationCheats>().SingleInstance();
        }
    }

    private UiDocument? _document;
    private UiPanel? _overlay;
    private UiText? _status;
    private UiText? _fps;
    private float _fpsElapsed;
    private int _fpsFrames;

    public override void OnInitialize()
    {
        _document = ui.Load("UI/CheatOverlay.xml");
        _document.Reloaded += BuildUi;
        BuildUi(_document);
    }

    public override void OnUnload()
    {
        if (_document is null)
            return;
        _document.Reloaded -= BuildUi;
        ui.Unload(_document);
        _document = null;
        _overlay = null;
        _status = null;
        _fps = null;
    }

    public override void OnUpdate(float deltaTime)
    {
        _fpsElapsed += deltaTime;
        _fpsFrames++;
        if (_fps is null || _fpsElapsed < 0.25f)
            return;
        var framesPerSecond = _fpsFrames / Math.Max(_fpsElapsed, 0.0001f);
        _fps.Value = $"{framesPerSecond:0} FPS";
        _fpsElapsed = 0f;
        _fpsFrames = 0;
    }

    private void BuildUi(UiDocument document)
    {
        var trigger = document.GetElementById<UiButton>("cheat-trigger");
        var close = document.GetElementById<UiButton>("cheat-close");
        _overlay = document.GetElementById<UiPanel>("cheat-overlay");
        _status = document.GetElementById<UiText>("cheat-status");
        _fps = document.GetElementById<UiText>("cheat-fps");
        var groups = document.GetElementById<UiPanel>("cheat-groups");
        groups.Clear();
        foreach (var group in registry.Actions.GroupBy(action => action.Group))
            AddGroup(document, groups, group.Key, group);
        trigger.Clicked += _ => ShowOverlay(true);
        close.Clicked += _ => ShowOverlay(false);
        ShowOverlay(false);
    }

    private void AddGroup(
        UiDocument document,
        UiPanel parent,
        string title,
        IEnumerable<CheatAction> actions)
    {
        var group = document.CreatePanel(new Dictionary<string, string> { ["class"] = "cheat-group" });
        group.Add(document.CreateText(title, new Dictionary<string, string> { ["class"] = "cheat-group-title" }));
        var grid = document.CreatePanel(new Dictionary<string, string> { ["class"] = "cheat-button-grid" });
        foreach (var action in actions)
        {
            var button = document.CreateButton(action.Title, new Dictionary<string, string> { ["class"] = "cheat-action" });
            button.Clicked += _ => RunAction(action);
            grid.Add(button);
        }
        group.Add(grid);
        parent.Add(group);
    }

    private void RunAction(CheatAction action)
    {
        try
        {
            _status!.Value = action.Invoke();
        }
        catch (Exception exception)
        {
            _status!.Value = exception.InnerException?.Message ?? exception.Message;
        }
    }

    private void ShowOverlay(bool visible)
    {
        if (_overlay is null)
            return;
        _overlay.IsVisible = visible;
        if (visible && _status is not null)
            _status.Value = $"{registry.Actions.Count} cheats";
    }
}
