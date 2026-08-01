using System.Numerics;
using Vecxy.Assets;
using Vecxy.Kernel;

namespace Game.Elevator.InteractiveMap;

public sealed class MapRegion
{
    public required int Id { get; init; }
    public required string Name { get; init; }
    public required Color32 MaskColor { get; init; }
}

public sealed class MapPoint
{
    public required int Index { get; init; }
    public required Vector2 NormalizedPosition { get; init; }
    public required Vector2 SourcePixelPosition { get; init; }
    public required RectInt SourceBounds { get; init; }
    public required int PixelCount { get; init; }
}

public readonly record struct MapDebugSample(
    Vector2 Uv,
    int PixelX,
    int PixelY,
    Color32 PixelColor,
    MapRegion? Region,
    bool IsInsideMap);

public interface IInteractiveMap
{
    MapRegion? HoveredRegion { get; }
    IReadOnlyList<MapRegion> Regions { get; }
    IReadOnlyList<MapPoint> SecretPoints { get; }
    Rect MapRect { get; }
    MapDebugSample DebugSample { get; }
    bool DebugEnabled { get; set; }
    bool IsVisible { get; set; }

    Vector2 MapToScreen(Vector2 normalizedPosition);
    Vector2 ScreenToMap(Vector2 screenPosition);

    event Action<MapRegion>? RegionEntered;
    event Action<MapRegion>? RegionExited;
    event Action<MapRegion>? RegionClicked;
}
