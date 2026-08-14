using System.Globalization;

namespace HardCore.Cultivation.Game.Application;

public static class MoneyFormatter
{
    private static readonly string[] Suffixes = [string.Empty, "К", "КК", "ММ", "МММ", "ММММ", "МММММ"];

    public static string Format(long amount)
    {
        var negative = amount < 0;
        var value = Math.Abs((decimal)amount);
        var suffixIndex = 0;
        while (value >= 1000m && suffixIndex < Suffixes.Length - 1)
        {
            value /= 1000m;
            suffixIndex++;
        }

        value = Math.Round(value, DecimalPlaces(value), MidpointRounding.AwayFromZero);
        if (value >= 1000m && suffixIndex < Suffixes.Length - 1)
        {
            value /= 1000m;
            suffixIndex++;
            value = Math.Round(value, DecimalPlaces(value), MidpointRounding.AwayFromZero);
        }

        var format = DecimalPlaces(value) switch { 2 => "0.##", 1 => "0.#", _ => "0" };
        var sign = negative ? "−" : string.Empty;
        var suffix = Suffixes[suffixIndex];
        return string.Concat(sign, value.ToString(format, CultureInfo.InvariantCulture),
            suffixIndex == 0 ? string.Empty : $" {suffix}");
    }

    private static int DecimalPlaces(decimal value) => value switch
    {
        >= 100m => 0,
        >= 10m => 1,
        _ => 2
    };
}
