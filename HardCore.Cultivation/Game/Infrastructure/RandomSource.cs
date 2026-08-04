namespace HardCore.Cultivation.Game.Infrastructure;

public interface IRandomSource
{
    int NextInt(int minInclusive, int maxExclusive);
    decimal NextDecimal(decimal minInclusive, decimal maxExclusive);
}

public sealed class SystemRandomSource : IRandomSource
{
    private readonly Random _random = new();

    public int NextInt(int minInclusive, int maxExclusive) =>
        _random.Next(minInclusive, maxExclusive);

    public decimal NextDecimal(decimal minInclusive, decimal maxExclusive)
    {
        if (maxExclusive <= minInclusive)
            throw new ArgumentOutOfRangeException(nameof(maxExclusive));
        return minInclusive + (decimal)_random.NextDouble() * (maxExclusive - minInclusive);
    }
}
