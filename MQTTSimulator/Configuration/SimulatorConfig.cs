namespace MQTTSimulator.Configuration;

public class SimulatorConfig
{
    public int ConnectionDelayMs { get; set; } = 0;
    public Dictionary<string, List<FieldConfig>> TelemetryProfiles { get; set; } = new();
    public List<DeviceConfig> Devices { get; set; } = new();
    public List<IoTHubFleetConfig> IoTHubFleets { get; set; } = new();
}
