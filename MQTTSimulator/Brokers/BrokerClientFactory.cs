using Microsoft.Extensions.Logging;
using MQTTSimulator.Configuration;

namespace MQTTSimulator.Brokers;

public static class BrokerClientFactory
{
    public static IBrokerClient Create(DeviceConfig deviceConfig, ILogger logger)
    {
        return deviceConfig.Broker.Type switch
        {
            BrokerType.IoTHub => new IoTHubBrokerClient(deviceConfig, logger),
            BrokerType.EventHub => new EventHubBrokerClient(deviceConfig, logger),
            BrokerType.MqttMtls => new MqttMtlsBrokerClient(deviceConfig, logger),
            BrokerType.Mqtt or BrokerType.MqttTls => new MqttBrokerClient(deviceConfig, logger),
            _ => throw new ArgumentException($"Unknown broker type: {deviceConfig.Broker.Type}")
        };
    }
}
