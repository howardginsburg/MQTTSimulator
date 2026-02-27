namespace MQTTSimulator.Configuration;

public class SimulatorConfig
{
    public int ConnectionDelayMs { get; set; } = 0;
    public string DefaultInterval { get; set; } = "5s";
    /// <summary>
    /// Maximum number of devices to show per page in the live dashboard.
    /// Set to 0 to auto-size based on terminal height. Default: 15.
    /// Use ← → arrow keys at runtime to page through devices.
    /// </summary>
    public int PageSize { get; set; } = 15;
    public Dictionary<string, BrokerConfig> Brokers { get; set; } = new();
    public Dictionary<string, Dictionary<string, FieldConfig>> Profiles { get; set; } = new();
    public List<DeviceConfig> Devices { get; set; } = new();
    public List<FleetConfig> Fleets { get; set; } = new();
}
