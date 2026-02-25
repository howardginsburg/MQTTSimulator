namespace MQTTSimulator.Configuration;

public class DeviceConfig
{
    public string DeviceId { get; set; } = string.Empty;
    public bool Enabled { get; set; } = true;
    public int SendIntervalMs { get; set; } = 5000;
    public string TelemetryProfileName { get; set; } = string.Empty;
    public BrokerConfig Broker { get; set; } = new();
}
