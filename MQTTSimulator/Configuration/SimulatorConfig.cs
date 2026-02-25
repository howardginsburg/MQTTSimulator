namespace MQTTSimulator.Configuration;

public class SimulatorConfig
{
    public int ConnectionDelayMs { get; set; } = 0;
    public string DefaultInterval { get; set; } = "5s";
    public Dictionary<string, Dictionary<string, FieldConfig>> Profiles { get; set; } = new();
    public List<DeviceConfig> Devices { get; set; } = new();
    public List<FleetConfig> Fleets { get; set; } = new();
}
