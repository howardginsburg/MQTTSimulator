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
        var port = _brokerConfig.Port > 0 ? _brokerConfig.Port : (_brokerConfig.Type == BrokerType.MqttTls ? 8883 : 1883);

        var optionsBuilder = new MqttClientOptionsBuilder()
            .WithTcpServer(_brokerConfig.Hostname, port)
            .WithClientId(_deviceConfig.DeviceId);

        if (!string.IsNullOrEmpty(_brokerConfig.Username))
        {
            optionsBuilder.WithCredentials(_brokerConfig.Username, _brokerConfig.Password);
        }

        if (_brokerConfig.Type == BrokerType.MqttTls)
        {
            var tlsOptionsBuilder = new MqttClientTlsOptionsBuilder()
                .UseTls(true);

            if (!string.IsNullOrEmpty(_brokerConfig.CaCertificatePath))
            {
                var caCert = LoadCertificateFromPem(_brokerConfig.CaCertificatePath);
                tlsOptionsBuilder.WithTrustChain(new X509Certificate2Collection(caCert));
            }

            optionsBuilder.WithTlsOptions(tlsOptionsBuilder.Build());
        }

        var response = await _client.ConnectAsync(optionsBuilder.Build(), cancellationToken);
        var protocol = _brokerConfig.Type == BrokerType.MqttTls ? "MQTT+TLS" : "MQTT";
        _logger.LogInformation("Device {DeviceId} connected to {Hostname}:{Port} via {Protocol} (ResultCode: {ResultCode})",
            _deviceConfig.DeviceId, _brokerConfig.Hostname, port, protocol, response.ResultCode);
    }

    public async Task SendAsync(string payload, CancellationToken cancellationToken = default)
    {
        var message = new MqttApplicationMessageBuilder()
            .WithTopic(_brokerConfig.Topic)
            .WithPayload(payload)
            .WithQualityOfServiceLevel(MQTTnet.Protocol.MqttQualityOfServiceLevel.AtLeastOnce)
            .WithRetainFlag(false)
            .Build();

        await _client.PublishAsync(message, cancellationToken);
        _logger.LogDebug("Device {DeviceId} sent {Bytes} bytes to {Topic}",
            _deviceConfig.DeviceId, payload.Length, _brokerConfig.Topic);
    }

    public async Task DisconnectAsync(CancellationToken cancellationToken = default)
    {
        if (_client.IsConnected)
        {
            await _client.DisconnectAsync(new MqttClientDisconnectOptionsBuilder().Build(), cancellationToken);
            _logger.LogInformation("Device {DeviceId} disconnected from {Hostname}",
                _deviceConfig.DeviceId, _brokerConfig.Hostname);
        }
    }

    private static X509Certificate2 LoadCertificateFromPem(string path)
    {
        var pem = File.ReadAllText(path);
        var base64 = pem
            .Replace("-----BEGIN CERTIFICATE-----", "")
            .Replace("-----END CERTIFICATE-----", "")
            .Replace("\r", "")
            .Replace("\n", "");
        return new X509Certificate2(Convert.FromBase64String(base64));
    }

    public async ValueTask DisposeAsync()
    {
        await DisconnectAsync();
        _client.Dispose();
    }
}
