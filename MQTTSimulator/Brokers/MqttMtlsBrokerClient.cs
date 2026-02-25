using System.Security.Cryptography.X509Certificates;
using Microsoft.Extensions.Logging;
using MQTTnet;
using MQTTnet.Client;
using MQTTSimulator.Configuration;

namespace MQTTSimulator.Brokers;

public class MqttMtlsBrokerClient : IBrokerClient
{
    private readonly IMqttClient _client;
    private readonly DeviceConfig _deviceConfig;
    private readonly BrokerConfig _brokerConfig;
    private readonly ILogger _logger;

    public MqttMtlsBrokerClient(DeviceConfig deviceConfig, ILogger logger)
    {
        _deviceConfig = deviceConfig;
        _brokerConfig = deviceConfig.Broker;
        _logger = logger;
        _client = new MqttFactory().CreateMqttClient();
    }

    public async Task ConnectAsync(CancellationToken cancellationToken = default)
    {
        using var ephemeralCert = X509Certificate2.CreateFromPemFile(
            _brokerConfig.CertificatePath,
            _brokerConfig.KeyPath);
        var clientCert = new X509Certificate2(ephemeralCert.Export(X509ContentType.Pfx));

        var tlsOptionsBuilder = new MqttClientTlsOptionsBuilder()
            .UseTls(true)
            .WithClientCertificates(new[] { clientCert });

        if (!string.IsNullOrEmpty(_brokerConfig.CaCertificatePath))
        {
            var caCert = LoadCertificateFromPem(_brokerConfig.CaCertificatePath);
            tlsOptionsBuilder.WithTrustChain(new X509Certificate2Collection(caCert));
        }

        var port = _brokerConfig.Port > 0 ? _brokerConfig.Port : 8883;
        var optionsBuilder = new MqttClientOptionsBuilder()
            .WithTcpServer(_brokerConfig.Hostname, port)
            .WithClientId(_deviceConfig.DeviceId)
            .WithCredentials(_deviceConfig.DeviceId)
            .WithTlsOptions(tlsOptionsBuilder.Build());

        var options = optionsBuilder.Build();

        var response = await _client.ConnectAsync(options, cancellationToken);
        _logger.LogInformation("Device {DeviceId} connected to {Hostname}:{Port} via mTLS (ResultCode: {ResultCode})",
            _deviceConfig.DeviceId, _brokerConfig.Hostname, port, response.ResultCode);
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
