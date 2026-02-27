namespace MQTTSimulator.Configuration;

public class DeviceConfig
{
    public string Id { get; set; } = string.Empty;
    public bool Enabled { get; set; } = true;
    public string? Interval { get; set; }
    public string Profile { get; set; } = string.Empty;
    public BrokerConfig Broker { get; set; } = new();

    // Set by SimulationHostedService when Interval is null
    internal string EffectiveInterval { get; set; } = "5s";

    // Fixed interval in ms (or minimum of a range) — used for display/logging
    public int IntervalMs => IntervalParser.ToMilliseconds(Interval ?? EffectiveInterval);

    // Returns a randomized delay if a range is configured (e.g. "90s-180s"), otherwise the fixed interval
    public int GetNextIntervalMs() => IntervalParser.GetNextMilliseconds(Interval ?? EffectiveInterval);
}
