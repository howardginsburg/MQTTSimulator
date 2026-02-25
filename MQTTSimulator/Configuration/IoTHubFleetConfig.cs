namespace MQTTSimulator.Configuration;

public class IoTHubFleetConfig
{
    public bool Enabled { get; set; } = true;
    public string ConnectionString { get; set; } = string.Empty;
    public string DevicePrefix { get; set; } = "device";
    public int DeviceCount { get; set; } = 1;
    public int SendIntervalMs { get; set; } = 5000;
    public string TelemetryProfileName { get; set; } = string.Empty;
}
