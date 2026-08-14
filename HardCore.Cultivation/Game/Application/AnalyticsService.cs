using Mediator.Net;
using Mediator.Net.Contracts;
using HardCore.Cultivation.Game.Infrastructure;

namespace HardCore.Cultivation.Game.Application;

public interface IAnalyticsService
{
    void Publish(AnalyticsEvent analyticsEvent);
}

public sealed class AnalyticsService(IMediator mediator, GameAnalyticsInfo analytics) : IAnalyticsService
{
    public void Publish(AnalyticsEvent analyticsEvent)
    {
        ArgumentNullException.ThrowIfNull(analyticsEvent);
        if (OperatingSystem.IsAndroid() && !analytics.IsAppMetricaEnabled)
            return;

        // Analytics delivery is asynchronous and must never block gameplay.
        _ = mediator.PublishAsync(analyticsEvent);
    }
}

public sealed class AnalyticsEvent : IEvent
{
    public AnalyticsEvent(string name, params (string Key, object? Value)[] parameters)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        Name = name;
        Parameters = parameters.ToDictionary(parameter => parameter.Key, parameter => parameter.Value,
            StringComparer.Ordinal);
    }

    public string Name { get; }
    public IReadOnlyDictionary<string, object?> Parameters { get; }
}

public static class AnalyticsEventExtensions
{
    private static IAnalyticsService? _service;

    public static void Bind(IAnalyticsService service)
    {
        ArgumentNullException.ThrowIfNull(service);
        Interlocked.Exchange(ref _service, service);
    }

    public static void Unbind() => Interlocked.Exchange(ref _service, null);

    public static void Publish(this AnalyticsEvent analyticsEvent)
    {
        ArgumentNullException.ThrowIfNull(analyticsEvent);
        Volatile.Read(ref _service)?.Publish(analyticsEvent);
    }
}
