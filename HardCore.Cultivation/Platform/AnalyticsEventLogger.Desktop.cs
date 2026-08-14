#if !ANDROID
using System.Text.Json;
using HardCore.Cultivation.Game.Application;
using Mediator.Net;
using Mediator.Net.Context;
using Mediator.Net.Contracts;
using Vecxy.Diagnostics;

namespace HardCore.Cultivation.Platform;

public sealed class AnalyticsEventLogger : IEventHandler<AnalyticsEvent>
{
    public Task Handle(IReceiveContext<AnalyticsEvent> context, CancellationToken cancellationToken)
    {
        var analyticsEvent = context.Message;
        Logger.Info($"[Analytics] {analyticsEvent.Name} {JsonSerializer.Serialize(analyticsEvent.Parameters)}");
        return Task.CompletedTask;
    }
}
#endif
