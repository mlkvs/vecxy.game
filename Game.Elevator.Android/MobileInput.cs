using System.Threading;

namespace Game.Elevator;

internal static class MobileInput
{
    private static int _mapToggleRequested;

    public static void RequestMapToggle() =>
        Interlocked.Exchange(ref _mapToggleRequested, 1);

    public static bool ConsumeMapToggle() =>
        Interlocked.Exchange(ref _mapToggleRequested, 0) != 0;
}
