namespace MQTTSimulator.Simulation;

public class DeviceState
{
    public string DeviceId { get; set; } = string.Empty;
    public string BrokerType { get; set; } = string.Empty;
    public string Status { get; set; } = "Starting";
    public long MessagesSent { get; set; }
    public string LastSendTime { get; set; } = "-";
    public string LastPayload { get; set; } = string.Empty;
}
