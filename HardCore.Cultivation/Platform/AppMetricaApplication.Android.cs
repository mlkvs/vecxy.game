#if ANDROID
using System.Reflection;
using IO.Appmetrica.Analytics;

namespace HardCore.Cultivation.Platform;

[global::Android.App.Application(Name = "game.vecxy.hardcorecultivation.AppMetricaApplication")]
public class AppMetricaApplication : global::Android.App.Application
{
    public AppMetricaApplication()
    {
    }

    protected AppMetricaApplication(IntPtr handle, global::Android.Runtime.JniHandleOwnership transfer)
        : base(handle, transfer)
    {
    }

    public override void OnCreate()
    {
        base.OnCreate();

        var apiKey = typeof(AppMetricaApplication).Assembly
            .GetCustomAttributes<AssemblyMetadataAttribute>()
            .FirstOrDefault(attribute => attribute.Key == "AppMetricaApiKey")?.Value;
        if (string.IsNullOrWhiteSpace(apiKey))
            return;

        var config = AppMetricaConfig.NewConfigBuilder(apiKey).Build();
        AppMetrica.Activate(this, config);
    }
}
#endif
