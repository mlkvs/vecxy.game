#if ANDROID
using IO.Appmetrica.Analytics;

namespace HardCore.Cultivation.Platform;

[global::Android.App.Application]
[global::Android.Runtime.Preserve(AllMembers = true)]
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

        var apiKey = ReadApiKey();
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            global::Android.Util.Log.Warn("HardCore.Cultivation", "AppMetrica is disabled: appmetrica.apiKey is empty.");
            return;
        }

        var config = AppMetricaConfig.NewConfigBuilder(apiKey).Build();
        AppMetrica.Activate(this, config);
        global::Android.Util.Log.Info("HardCore.Cultivation", "AppMetrica activated.");
    }

    private string ReadApiKey()
    {
        try
        {
            using var stream = Assets!.Open("Configs/Analytics.yaml");
            using var reader = new StreamReader(stream);

            while (reader.ReadLine() is { } line)
            {
                var value = line.Trim();
                if (!value.StartsWith("apiKey:", StringComparison.Ordinal))
                    continue;

                return value["apiKey:".Length..].Trim().Trim('\'', '"');
            }
        }
        catch (IOException exception)
        {
            global::Android.Util.Log.Warn("HardCore.Cultivation", $"Unable to read Analytics.yaml: {exception.Message}");
        }

        return string.Empty;
    }
}
#endif
