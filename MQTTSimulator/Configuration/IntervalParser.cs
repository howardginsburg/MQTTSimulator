namespace MQTTSimulator.Configuration;

public static class IntervalParser
{
    public static int ToMilliseconds(string interval)
    {
        if (string.IsNullOrWhiteSpace(interval)) return 5000;
        interval = interval.Trim();

        if (interval.EndsWith("ms", StringComparison.OrdinalIgnoreCase))
            return int.Parse(interval[..^2]);

        if (interval.EndsWith("m", StringComparison.OrdinalIgnoreCase))
            return (int)(double.Parse(interval[..^1]) * 60_000);

        if (interval.EndsWith("s", StringComparison.OrdinalIgnoreCase))
            return (int)(double.Parse(interval[..^1]) * 1000);

        return int.Parse(interval);
    }
}
