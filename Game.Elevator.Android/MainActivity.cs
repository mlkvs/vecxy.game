using Android.App;
using Android.Content.PM;
using Android.OS;
using Android.Views;
using Game.Elevator.Android;
using Silk.NET.Windowing.Sdl.Android;
using Vecxy.Assets;
using Vecxy.Engine;
using Vecxy.Kernel;

namespace Game.Elevator;

[Activity(
    Label = "@string/app_name",
    MainLauncher = true,
    Exported = true,
    HardwareAccelerated = true,
    ScreenOrientation = ScreenOrientation.Landscape,
    ConfigurationChanges = SilkActivity.ConfigChangesFlags,
    Theme = "@android:style/Theme.Black.NoTitleBar.Fullscreen")]
public sealed class MainActivity : SilkActivity
{
    public override void LoadLibraries()
    {
        base.LoadLibraries();
    }

    public override void SetOrientationBis(
        int width,
        int height,
        bool resizable,
        string hint)
    {
        base.SetOrientationBis(width, height, resizable, hint);
    }

    protected override void OnRun()
    {
        var filesDirectory = FilesDir?.AbsolutePath ??
            throw new InvalidOperationException("Android files directory is unavailable.");
        var assetManager = Assets ??
            throw new InvalidOperationException("Android asset manager is unavailable.");
        var assetsDirectory = AndroidAssetExtractor.Extract(assetManager, filesDirectory);

        var options = new Engine.Options
        {
            Window = new IWindow.Options("Elevator", 1920, 1080),
            TargetFrameRate = 60
        };

        var layers = new List<AAppLayer.IDefinition>
        {
            new EngineLayer.Definition(
                new AssetsModule.Options
                {
                    AssetsDirectory = assetsDirectory
                }),
            new GameLayer.Definition()
        };

        using var engine = new Engine(options, layers);
        engine.Run();
    }

    protected override void OnDestroy()
    {
        base.OnDestroy();
    }

#pragma warning disable CS0672
    public override void OnBackPressed()
    {
        MobileInput.RequestMapToggle();
    }
#pragma warning restore CS0672
}
