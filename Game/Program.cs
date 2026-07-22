using Vecxy.Engine;

var options = new EngineOptions
{
    WindowTitle = "Game",
    AssetsPath = Path.Combine(AppContext.BaseDirectory, "Assets")
};

using var engine = new Engine(options, []);
engine.Run();
