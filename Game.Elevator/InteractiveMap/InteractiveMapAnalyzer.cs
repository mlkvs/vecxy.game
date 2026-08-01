using System.Numerics;
using Vecxy.Assets;
using Vecxy.Kernel;

namespace Game.Elevator.InteractiveMap;

public static class InteractiveMapAnalyzer
{
    public const int DefaultSecretColorTolerance = 4;
    public const int DefaultMinimumSecretPixelCount = 10;
    public const int DefaultLargeClusterWarningSize = 50_000;

    public static void ValidateIdenticalDimensions(
        params TextureAsset[] textures)
    {
        ArgumentNullException.ThrowIfNull(textures);
        if (textures.Length == 0)
            throw new ArgumentException("At least one texture is required.", nameof(textures));

        var width = textures[0].Width;
        var height = textures[0].Height;
        if (textures.Any(texture =>
                texture.Width != width ||
                texture.Height != height))
        {
            throw new InvalidDataException(
                "Map textures must have identical dimensions.");
        }
    }

    public static IReadOnlyDictionary<int, int> CountRegionPixels(
        TextureAsset mask,
        IReadOnlyList<MapRegion> regions,
        int colorTolerance = 2,
        byte alphaThreshold = 128)
    {
        ArgumentNullException.ThrowIfNull(mask);
        ArgumentNullException.ThrowIfNull(regions);

        var counts = regions.ToDictionary(region => region.Id, _ => 0);
        var exact = regions.ToDictionary(
            region => region.MaskColor.PackedRgb,
            region => region);
        var knownPixels = 0;

        for (var index = 0; index < mask.Pixels.Length; index += 4)
        {
            if (mask.Pixels[index + 3] < alphaThreshold)
                continue;

            var color = new Color32(
                mask.Pixels[index],
                mask.Pixels[index + 1],
                mask.Pixels[index + 2],
                mask.Pixels[index + 3]);

            MapRegion? region = null;
            if (!exact.TryGetValue(color.PackedRgb, out region))
            {
                region = regions.FirstOrDefault(candidate =>
                    color.IsNearRgb(candidate.MaskColor, colorTolerance));
            }

            if (region is null)
                continue;

            counts[region.Id]++;
            knownPixels++;
        }

        if (knownPixels == 0)
        {
            throw new InvalidDataException(
                "Mask.png does not contain any registered region colors.");
        }

        return counts;
    }

    public static IReadOnlyList<MapPoint> FindSecretPoints(
        TextureAsset secrets,
        Color32? markerColor = null,
        int colorTolerance = DefaultSecretColorTolerance,
        byte alphaThreshold = 128,
        int minimumPixelCount = DefaultMinimumSecretPixelCount,
        int largeClusterWarningSize = DefaultLargeClusterWarningSize,
        Action<string>? warning = null)
    {
        ArgumentNullException.ThrowIfNull(secrets);
        if (colorTolerance < 0)
            throw new ArgumentOutOfRangeException(nameof(colorTolerance));
        if (minimumPixelCount <= 0)
            throw new ArgumentOutOfRangeException(nameof(minimumPixelCount));

        var target = markerColor ?? new Color32(255, 0, 16, 255);
        var width = secrets.Width;
        var height = secrets.Height;
        var visited = new bool[checked(width * height)];
        var queue = new int[visited.Length];
        var clusters = new List<Cluster>();

        for (var start = 0; start < visited.Length; start++)
        {
            if (visited[start])
                continue;

            visited[start] = true;
            if (!IsMarkerPixel(secrets.Pixels, start, target, colorTolerance, alphaThreshold))
                continue;

            var head = 0;
            var tail = 0;
            queue[tail++] = start;

            long sumX = 0;
            long sumY = 0;
            var pixelCount = 0;
            var minX = width;
            var minY = height;
            var maxX = -1;
            var maxY = -1;

            while (head < tail)
            {
                var pixelIndex = queue[head++];
                var x = pixelIndex % width;
                var y = pixelIndex / width;

                sumX += x;
                sumY += y;
                pixelCount++;
                minX = Math.Min(minX, x);
                minY = Math.Min(minY, y);
                maxX = Math.Max(maxX, x);
                maxY = Math.Max(maxY, y);

                for (var offsetY = -1; offsetY <= 1; offsetY++)
                {
                    var neighborY = y + offsetY;
                    if ((uint)neighborY >= (uint)height)
                        continue;

                    for (var offsetX = -1; offsetX <= 1; offsetX++)
                    {
                        if (offsetX == 0 && offsetY == 0)
                            continue;

                        var neighborX = x + offsetX;
                        if ((uint)neighborX >= (uint)width)
                            continue;

                        var neighborIndex = neighborY * width + neighborX;
                        if (visited[neighborIndex])
                            continue;

                        visited[neighborIndex] = true;
                        if (IsMarkerPixel(
                                secrets.Pixels,
                                neighborIndex,
                                target,
                                colorTolerance,
                                alphaThreshold))
                        {
                            queue[tail++] = neighborIndex;
                        }
                    }
                }
            }

            if (pixelCount < minimumPixelCount)
                continue;

            if (pixelCount > largeClusterWarningSize)
            {
                warning?.Invoke(
                    $"Secret marker cluster contains {pixelCount} pixels; " +
                    "verify that markers do not touch each other.");
            }

            var center = new Vector2(
                (float)sumX / pixelCount,
                (float)sumY / pixelCount);
            clusters.Add(new Cluster(
                center,
                new RectInt(
                    minX,
                    minY,
                    maxX - minX + 1,
                    maxY - minY + 1),
                pixelCount));
        }

        return clusters
            .OrderBy(cluster => cluster.Center.Y)
            .ThenBy(cluster => cluster.Center.X)
            .Select((cluster, index) => new MapPoint
            {
                Index = index,
                SourcePixelPosition = cluster.Center,
                NormalizedPosition = new Vector2(
                    cluster.Center.X / width,
                    cluster.Center.Y / height),
                SourceBounds = cluster.Bounds,
                PixelCount = cluster.PixelCount
            })
            .ToArray();
    }

    private static bool IsMarkerPixel(
        byte[] pixels,
        int pixelIndex,
        Color32 target,
        int tolerance,
        byte alphaThreshold)
    {
        var index = pixelIndex * 4;
        return pixels[index + 3] >= alphaThreshold &&
               Math.Abs(pixels[index] - target.R) <= tolerance &&
               Math.Abs(pixels[index + 1] - target.G) <= tolerance &&
               Math.Abs(pixels[index + 2] - target.B) <= tolerance;
    }

    private readonly record struct Cluster(
        Vector2 Center,
        RectInt Bounds,
        int PixelCount);
}
