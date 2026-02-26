namespace MQTTSimulator.Configuration;

public enum BrokerType
{
    IoTHub,
    Mqtt,
    MqttTls,
    MqttMtls,
    EventHub
}

public enum AuthMethod
{
    SAS,
    X509
}

public class BrokerConfig
{
    /// <summary>When set, this broker config is a reference to a named broker in simulator.brokers.</summary>
    public string? Name { get; set; }

    internal const BrokerType DefaultType = BrokerType.Mqtt;
    public BrokerType Type { get; set; } = DefaultType;
    public string Host { get; set; } = string.Empty;
    public int Port { get; set; }
    public string Topic { get; set; } = string.Empty;

    public string Connection { get; set; } = string.Empty;

    public string User { get; set; } = string.Empty;
    public string Pass { get; set; } = string.Empty;

    public string Cert { get; set; } = string.Empty;
    public string Key { get; set; } = string.Empty;
    public string Ca { get; set; } = string.Empty;
    public string Hub { get; set; } = string.Empty;

    public AuthMethod Auth => !string.IsNullOrEmpty(Connection) ? AuthMethod.SAS
        : !string.IsNullOrEmpty(Cert) ? AuthMethod.X509
        : AuthMethod.SAS;

    public int EffectivePort => Port > 0 ? Port : Type switch
    {
        BrokerType.IoTHub or BrokerType.MqttTls or BrokerType.MqttMtls => 8883,
        _ => 1883
    };

    /// <summary>
    /// Returns the topic with <c>{deviceId}</c> replaced by the actual device ID.
    /// Use this everywhere a topic string is needed at runtime.
    /// </summary>
    public string ResolvedTopic(string deviceId) =>
        Topic.Replace("{deviceId}", deviceId, StringComparison.OrdinalIgnoreCase);
}
