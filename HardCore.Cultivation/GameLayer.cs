using Autofac;
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
    CultivationInteraction cultivationInteraction
) : AAppLayer
{
    private UiDocument? _document;
    private float _qi = 22.3f;
    private int _stones = 240;

    public class Definition : ADefinition<GameLayer>
    {
        public override void RegisterGlobal(ContainerBuilder builder)
        {
            builder.RegisterType<MenuScene>().AsSelf();
            builder
                .RegisterType<CultivationInteraction>()
                .AsSelf()
                .SingleInstance();
        }
    }

    public override void OnInitialize()
    {
        cultivationInteraction.CharacterClicked += Cultivate;
        window.SetCursorCaptured(false);
        scenes.LoadScene<MenuScene>();

        _document = ui.Load("UI/cultivation.xml");
        _document.Reloaded += BindUi;
        BindUi(_document);
    }

    public override void OnUnload()
    {
        cultivationInteraction.CharacterClicked -= Cultivate;
        if (_document is null)
            return;

        _document.Reloaded -= BindUi;
        ui.Unload(_document);
        _document = null;
    }

    private void BindUi(UiDocument document)
    {
        Bind(document, "#profile", () => Show("SOUL AND REALM"));
        Bind(document, "#sect", () => Show("SECT HALL"));
        Bind(document, "#body", () => Show("BODY REFINING"));
        Bind(document, "#manual", () => Show("CULTIVATION MANUAL"));
        Bind(document, "#inventory", () => Show("SPIRIT BAG"));
        Bind(document, "#treasure", () => Show("TREASURE PAVILION"));
        Bind(document, "#beast", () => Show("SPIRIT BEAST"));
        Bind(document, "#skill", () => Show("SECRET SKILLS"));
        UpdateResources();
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
