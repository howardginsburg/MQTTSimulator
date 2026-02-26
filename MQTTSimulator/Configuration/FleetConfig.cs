namespace MQTTSimulator.Configuration;

public class FleetConfig
{
    public BrokerType Type { get; set; }
    public bool Enabled { get; set; } = true;
    /// <summary>Reference to a named broker in simulator.brokers. When set, Type/Connection/Hub are inherited from that broker.</summary>
    public string? BrokerRef { get; set; }
    public string Connection { get; set; } = string.Empty;
    public string Hub { get; set; } = string.Empty;
    public string Prefix { get; set; } = "device";
    public int Count { get; set; } = 1;
    public string? Interval { get; set; }
    public string Profile { get; set; } = string.Empty;

    // Set by SimulationHostedService when Interval is null
    internal string EffectiveInterval { get; set; } = "5s";
    public int IntervalMs => IntervalParser.ToMilliseconds(Interval ?? EffectiveInterval);
}
