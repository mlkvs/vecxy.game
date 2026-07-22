using System;
using System.IO;
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

var gameLayer = new GameLayer();

using var engine = new Engine(options, [gameLayer]);
engine.Run();
