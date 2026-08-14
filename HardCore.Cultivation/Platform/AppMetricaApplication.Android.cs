#if ANDROID
using IO.Appmetrica.Analytics;

namespace HardCore.Cultivation.Platform;

public static class AppMetricaBootstrap
{
    public static void Activate(string apiKey)
    {
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            global::Android.Util.Log.Warn("HardCore.Cultivation", "AppMetrica is disabled: appmetrica.apiKey is empty.");
            return;
        }

        var config = AppMetricaConfig.NewConfigBuilder(apiKey).Build();
        AppMetrica.Activate(global::Android.App.Application.Context!, config);
        global::Android.Util.Log.Info("HardCore.Cultivation", "AppMetrica activated.");
    }
}
#endif
