#if ANDROID
using System.Text.Json;
using HardCore.Cultivation.Game.Application;
using IO.Appmetrica.Analytics;
using Mediator.Net;
using Mediator.Net.Context;
using Mediator.Net.Contracts;

namespace HardCore.Cultivation.Platform;

public sealed class AppMetricaAnalyticsEventHandler : IEventHandler<AnalyticsEvent>
{
    public Task Handle(IReceiveContext<AnalyticsEvent> context, CancellationToken cancellationToken)
    {
        var analyticsEvent = context.Message;
        AppMetrica.ReportEvent(analyticsEvent.Name, JsonSerializer.Serialize(analyticsEvent.Parameters));
        return Task.CompletedTask;
    }
}
#endif
