using Android.App;
using Android.Content.PM;
using Android.Graphics;
using Android.OS;
using Android.Util;
using Android.Views;
using Android.Widget;
using Android.Window;
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
    EnableOnBackInvokedCallback = true,
    ScreenOrientation = ScreenOrientation.Landscape,
    ConfigurationChanges = SilkActivity.ConfigChangesFlags,
    Theme = "@android:style/Theme.Black.NoTitleBar.Fullscreen")]
public sealed class MainActivity : SilkActivity
{
    private const string LogTag = "Vecxy.Elevator";
    private IOnBackInvokedCallback? _backCallback;

    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);

        if (!OperatingSystem.IsAndroidVersionAtLeast(33))
            return;

        _backCallback = new MapBackCallback();
        OnBackInvokedDispatcher.RegisterOnBackInvokedCallback(
            IOnBackInvokedDispatcher.PriorityDefault,
            _backCallback);
    }

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
        var phase = "Preparing Android storage";

        try
        {
            var filesDirectory = FilesDir?.AbsolutePath ??
                throw new InvalidOperationException("Android files directory is unavailable.");
            var assetManager = Assets ??
                throw new InvalidOperationException("Android asset manager is unavailable.");

            phase = "Extracting game assets";
            var assetsDirectory = AndroidAssetExtractor.Extract(assetManager, filesDirectory);

            phase = "Creating the engine";
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

            phase = "Initializing and running the engine";
            engine.Run();
        }
        catch (Exception exception)
        {
            ShowFatalError(phase, exception);
        }
    }

    private void ShowFatalError(string phase, Exception exception)
    {
        var details =
            $"Startup phase: {phase}\n\n{exception.GetType().FullName}: " +
            $"{exception.Message}\n\n{exception.StackTrace}";

        Log.Error(LogTag, details);

        try
        {
            var filesDirectory = FilesDir?.AbsolutePath;
            if (!string.IsNullOrWhiteSpace(filesDirectory))
                File.WriteAllText(System.IO.Path.Combine(filesDirectory, "elevator-crash.txt"), details);
        }
        catch
        {
            // The on-screen report remains available if writing the log fails.
        }

        using var dismissed = new ManualResetEventSlim();
        RunOnUiThread(() =>
        {
            var message = new TextView(this)
            {
                Text = details,
                TextSize = 13.0f,
                Typeface = Typeface.Monospace
            };
            message.SetTextColor(Color.White);
            message.SetPadding(32, 24, 32, 24);

            var scroll = new ScrollView(this);
            scroll.AddView(message);

            var builder = new AlertDialog.Builder(this);
            builder.SetTitle("Elevator startup error");
            builder.SetView(scroll);
            builder.SetPositiveButton("Close", (_, _) => dismissed.Set());
            builder.SetCancelable(false);

            var dialog = builder.Create();
            if (dialog is null)
            {
                dismissed.Set();
                return;
            }

            dialog.Show();
        });

        dismissed.Wait();
    }

    protected override void OnDestroy()
    {
        if (_backCallback is not null &&
            OperatingSystem.IsAndroidVersionAtLeast(33))
        {
            OnBackInvokedDispatcher.UnregisterOnBackInvokedCallback(_backCallback);
            _backCallback.Dispose();
            _backCallback = null;
        }

        base.OnDestroy();
    }

    public override bool DispatchKeyEvent(KeyEvent? eventData)
    {
        if (eventData?.KeyCode != Keycode.VolumeDown)
            return base.DispatchKeyEvent(eventData);

        if (eventData.Action == KeyEventActions.Down && eventData.RepeatCount == 0)
            MobileInput.RequestMapToggle();

        return true;
    }

#pragma warning disable CS0672
    public override void OnBackPressed()
    {
        MobileInput.RequestMapToggle();
    }
#pragma warning restore CS0672

    private sealed class MapBackCallback : Java.Lang.Object, IOnBackInvokedCallback
    {
        public void OnBackInvoked() => MobileInput.RequestMapToggle();
    }
}
