namespace MQTTSimulator.Configuration;

public static class IntervalParser
{
    /// <summary>
    /// Returns the fixed interval in milliseconds, or the minimum of a range.
    /// Use <see cref="GetNextMilliseconds"/> to get a randomized value for range intervals.
    /// </summary>
    public static int ToMilliseconds(string interval)
    {
        if (string.IsNullOrWhiteSpace(interval)) return 5000;
        var (min, _) = ParseRange(interval.Trim());
        return min;
    }

    /// <summary>
    /// Returns a random value within [min, max] for range intervals (e.g. "90s-180s"),
    /// or the fixed value for single intervals (e.g. "30s").
    /// </summary>
    public static int GetNextMilliseconds(string interval)
    {
        if (string.IsNullOrWhiteSpace(interval)) return 5000;
        var (min, max) = ParseRange(interval.Trim());
        return max.HasValue ? Random.Shared.Next(min, max.Value + 1) : min;
    }

    public static bool IsRange(string interval)
    {
        if (string.IsNullOrWhiteSpace(interval)) return false;
        var (_, max) = ParseRange(interval.Trim());
        return max.HasValue;
    }

    private static (int min, int? max) ParseRange(string interval)
    {
        var dashIdx = FindRangeSeparatorIndex(interval);
        if (dashIdx > 0)
        {
            var part1 = interval[..dashIdx].Trim();
            var part2 = interval[(dashIdx + 1)..].Trim();
            if (!string.IsNullOrEmpty(part1) && !string.IsNullOrEmpty(part2))
            {
                int a = ParseSingle(part1);
                int b = ParseSingle(part2);
                return (Math.Min(a, b), Math.Max(a, b));
            }
        }
        return (ParseSingle(interval), null);
    }

    // Finds the index of '-' that acts as a range separator.
    // Valid: the character before '-' is a digit or a known suffix (s, m).
    private static int FindRangeSeparatorIndex(string interval)
    {
        for (int i = 1; i < interval.Length - 1; i++)
        {
            if (interval[i] != '-') continue;
            char prev = interval[i - 1];
            if (char.IsDigit(prev) || prev == 's' || prev == 'm')
                return i;
        }
        return -1;
    }

    private static int ParseSingle(string interval)
    {
        if (interval.EndsWith("ms", StringComparison.OrdinalIgnoreCase))
            return int.Parse(interval[..^2]);
        if (interval.EndsWith("m", StringComparison.OrdinalIgnoreCase))
            return (int)(double.Parse(interval[..^1]) * 60_000);
        if (interval.EndsWith("s", StringComparison.OrdinalIgnoreCase))
            return (int)(double.Parse(interval[..^1]) * 1_000);
        return int.Parse(interval);
    }
}
