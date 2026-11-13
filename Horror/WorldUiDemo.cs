using Vecxy.Scene;
using Vecxy.Physics;
using Vecxy.UI;

namespace Horror;

/// <summary>Example bindings shared by mouse clicks, the E key and explicit UI ray activations.</summary>
public sealed class WorldUiDemo(IUiManager ui,IPhysicsSystem physics) : AComponent
{
    private UiDocument? _crosshair, _status, _console;
    private UiCanvas? _surfaceCanvas;
    private LaundryMachine? _machine;
    private int _clicks;

    public override void Start()
    {
        _crosshair = ui.Load("UI/Crosshair.xml");
        _machine = SceneInstance.Objects.SelectMany(o => o.Components).OfType<LaundryMachine>().FirstOrDefault(m => m.SceneObject?.Name == "Washer_01");
        var player = SceneInstance.Objects.Select(o=>o.GetComponent<Player>()).OfType<Player>().FirstOrDefault();
        foreach (var canvas in SceneInstance.Objects.SelectMany(o => o.Components).OfType<UiCanvas>())
        {
            canvas.IsRayOccluded = (origin,direction,distance) => distance > .005f && physics.Raycast(origin,direction,distance-.005f,player?.SceneObject,out _);
            if (canvas.Document?.Path == "UI/WasherStatus.xml") _status = canvas.Document;
            if (canvas.Document?.Path == "UI/WasherConsole.xml") { _console = canvas.Document; _surfaceCanvas = canvas; }
        }
        if (_status is not null) { _status.Reloaded += BindStatus; BindStatus(_status); }
        if (_console is not null) { _console.Reloaded += BindConsole; BindConsole(_console); }
    }

    private void BindStatus(UiDocument document)
    {
        document.GetElementById<UiButton>("power").Clicked += TogglePower;
        UpdateStatus();
    }

    private void TogglePower(UiElement element)
    {
        if (_machine is { IsDestroyed: false }) _machine.TogglePower();
        UpdateStatus();
    }

    private void UpdateStatus()
    {
        if (_status is null) return;
        var powered = _machine?.Powered ?? false;
        _status.GetElementById<UiText>("status").Value = powered ? "Питание включено" : "Питание выключено";
        _status.GetElementById<UiButton>("power").Label = powered ? "ВЫКЛЮЧИТЬ" : "ВКЛЮЧИТЬ";
        if (powered) _status.Root.RemoveClass("off"); else _status.Root.AddClass("off");
    }

    private void BindConsole(UiDocument document)
    {
        document.GetElementById<UiButton>("console-action").Clicked += ClickConsole;
        UpdateConsole();
    }

    private void ClickConsole(UiElement element) { _clicks++; UpdateConsole(); }
    private void UpdateConsole()
    {
        if (_console is null) return;
        _console.GetElementById<UiText>("clicks").Value = $"Нажатий: {_clicks}";
        if ((_clicks & 1) != 0) _console.Root.AddClass("alternate"); else _console.Root.RemoveClass("alternate");
    }

    public override void Update(float deltaTime)
    {
        if (_crosshair is not null) _crosshair.IsVisible = _surfaceCanvas?.CapturesMouseLook != true;
    }

    public override void OnDestroy()
    {
        if (_status is not null) _status.Reloaded -= BindStatus;
        if (_console is not null) _console.Reloaded -= BindConsole;
        if (_crosshair is not null) ui.Unload(_crosshair);
        _crosshair = _status = _console = null;
    }
}
