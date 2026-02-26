using System.Security.Cryptography.X509Certificates;
using Microsoft.Extensions.Logging;
using MQTTnet;
using MQTTnet.Client;
using MQTTSimulator.Configuration;

namespace MQTTSimulator.Brokers;

public class MqttBrokerClient : IBrokerClient
{
    private readonly IMqttClient _client;
    private readonly DeviceConfig _deviceConfig;
    private readonly BrokerConfig _brokerConfig;
    private readonly ILogger _logger;

    public MqttBrokerClient(DeviceConfig deviceConfig, ILogger logger)
    {
        _deviceConfig = deviceConfig;
        _brokerConfig = deviceConfig.Broker;
        _logger = logger;
        _client = new MqttFactory().CreateMqttClient();
    }

    public async Task ConnectAsync(CancellationToken cancellationToken = default)
    {
        var port = _brokerConfig.EffectivePort;

        var optionsBuilder = new MqttClientOptionsBuilder()
            .WithTcpServer(_brokerConfig.Host, port)
            .WithClientId(_deviceConfig.Id);

        if (!string.IsNullOrEmpty(_brokerConfig.User))
        {
            optionsBuilder.WithCredentials(_brokerConfig.User, _brokerConfig.Pass);
        }

        if (_brokerConfig.Type == BrokerType.MqttTls)
        {
            var tlsOptionsBuilder = new MqttClientTlsOptionsBuilder()
                .UseTls(true);

            if (!string.IsNullOrEmpty(_brokerConfig.Ca))
            {
                var caCert = CertificateHelper.LoadFromPem(_brokerConfig.Ca);
                tlsOptionsBuilder.WithTrustChain(new X509Certificate2Collection(caCert));
            }

            optionsBuilder.WithTlsOptions(tlsOptionsBuilder.Build());
        }

        var response = await _client.ConnectAsync(optionsBuilder.Build(), cancellationToken);
        var protocol = _brokerConfig.Type == BrokerType.MqttTls ? "MQTT+TLS" : "MQTT";
        _logger.LogInformation("Device {DeviceId} connected to {Hostname}:{Port} via {Protocol} (ResultCode: {ResultCode})",
            _deviceConfig.Id, _brokerConfig.Host, port, protocol, response.ResultCode);
    }

    public async Task SendAsync(string payload, CancellationToken cancellationToken = default)
    {
        var topic = _brokerConfig.ResolvedTopic(_deviceConfig.Id);
        var message = new MqttApplicationMessageBuilder()
            .WithTopic(topic)
            .WithPayload(payload)
            .WithQualityOfServiceLevel(MQTTnet.Protocol.MqttQualityOfServiceLevel.AtLeastOnce)
            .WithRetainFlag(false)
            .Build();

        await _client.PublishAsync(message, cancellationToken);
        _logger.LogDebug("Device {DeviceId} sent {Bytes} bytes to {Topic}",
            _deviceConfig.Id, payload.Length, topic);
    }

    public async Task DisconnectAsync(CancellationToken cancellationToken = default)
    {
        if (_client.IsConnected)
        {
            await _client.DisconnectAsync(new MqttClientDisconnectOptionsBuilder().Build(), cancellationToken);
            _logger.LogInformation("Device {DeviceId} disconnected from {Hostname}",
                _deviceConfig.Id, _brokerConfig.Host);
        }
    }

    public async ValueTask DisposeAsync()
    {
        await DisconnectAsync();
        _client.Dispose();
    }
}
