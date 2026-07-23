using Game;
using Vecxy.Engine;

var assetsPath = Path.Combine(AppContext.BaseDirectory, "Assets");
#if DEBUG
assetsPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../Assets"));
#endif


var options = new EngineOptions
{
    WindowTitle = "Game",
    AssetsPath = assetsPath
};

var layers = new List<AppLayer>
{
    new GameLayer(),
    new EditorLayer(),
    new EngineLayer()
};

using var engine = new Engine(options, layers);

engine.Run();
