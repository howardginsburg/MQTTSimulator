using System.Text.Json.Serialization;

namespace MQTTSimulator.Configuration;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum BrokerType
{
    IoTHub,
    Mqtt,
    MqttTls,
    MqttMtls
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum AuthMethod
{
    SAS,
    X509
}

public class BrokerConfig
{
    public BrokerType Type { get; set; } = BrokerType.Mqtt;
    public string Hostname { get; set; } = string.Empty;
    public int Port { get; set; } = 1883;
    public string Topic { get; set; } = string.Empty;

    // IoT Hub auth
    public AuthMethod AuthMethod { get; set; } = AuthMethod.SAS;
    public string ConnectionString { get; set; } = string.Empty;

    // Username/password auth (Mqtt, MqttTls)
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;

    // Certificate paths (PEM format)
    public string CertificatePath { get; set; } = string.Empty;
    public string KeyPath { get; set; } = string.Empty;
    public string CaCertificatePath { get; set; } = string.Empty;
}
