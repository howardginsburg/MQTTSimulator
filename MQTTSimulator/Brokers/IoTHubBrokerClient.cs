using System.Security.Cryptography.X509Certificates;
using Microsoft.Extensions.Logging;
using MQTTnet;
using MQTTnet.Client;
using MQTTnet.Formatter;
using MQTTSimulator.Configuration;

namespace MQTTSimulator.Brokers;

public class IoTHubBrokerClient : IBrokerClient
{
    private readonly IMqttClient _client;
    private readonly DeviceConfig _deviceConfig;
    private readonly BrokerConfig _brokerConfig;
    private readonly ILogger _logger;
    private readonly string _topic;
    private readonly TimeSpan _sasTokenTtl = TimeSpan.FromHours(1);

    // Parsed from connection string for SAS auth
    private readonly string _hostname;
    private readonly string _deviceId;
    private readonly string _deviceKey;

    public IoTHubBrokerClient(DeviceConfig deviceConfig, ILogger logger)
    {
        _deviceConfig = deviceConfig;
        _brokerConfig = deviceConfig.Broker;
        _logger = logger;
        _client = new MqttFactory().CreateMqttClient();

        if (_brokerConfig.Auth == AuthMethod.SAS && !string.IsNullOrEmpty(_brokerConfig.Connection))
        {
            // Full device connection string: HostName=...;DeviceId=...;SharedAccessKey=...
            var parts = ParseConnectionString(_brokerConfig.Connection);
            _hostname = parts.hostname;
            _deviceId = parts.deviceId;
            _deviceKey = parts.sharedAccessKey;
        }
        else if (_brokerConfig.Auth == AuthMethod.SAS)
        {
            // Named broker pattern: host from broker, SharedAccessKey in key, DeviceId inferred from device id
            _hostname = _brokerConfig.Host;
            _deviceId = deviceConfig.Id;
            _deviceKey = _brokerConfig.Key;
        }
        else
        {
            // X509
            _hostname = _brokerConfig.Host;
            _deviceId = deviceConfig.Id;
            _deviceKey = string.Empty;
        }

        _topic = $"devices/{_deviceId}/messages/events/";
    }

    private static (string hostname, string deviceId, string sharedAccessKey) ParseConnectionString(string connectionString)
    {
        var pairs = connectionString.Split(';')
            .Select(part => part.Split('=', 2))
            .Where(kv => kv.Length == 2)
            .ToDictionary(kv => kv[0].Trim(), kv => kv[1].Trim(), StringComparer.OrdinalIgnoreCase);

        if (!pairs.TryGetValue("HostName", out var hostname))
            throw new ArgumentException("Connection string missing 'HostName'");
        if (!pairs.TryGetValue("DeviceId", out var deviceId))
            throw new ArgumentException("Connection string missing 'DeviceId'");
        if (!pairs.TryGetValue("SharedAccessKey", out var key))
            throw new ArgumentException("Connection string missing 'SharedAccessKey'");

        return (hostname, deviceId, key);
    }

    public async Task ConnectAsync(CancellationToken cancellationToken = default)
    {
        var optionsBuilder = new MqttClientOptionsBuilder()
            .WithTcpServer(_hostname, 8883)
            .WithClientId(_deviceId)
            .WithProtocolVersion(MqttProtocolVersion.V311)
            .WithCleanSession(true);

        var username = $"{_hostname}/{_deviceId}/?api-version=2021-04-12";
        optionsBuilder.WithCredentials(username, GetPassword());

        var tlsOptions = new MqttClientTlsOptionsBuilder()
            .UseTls(true)
            .Build();

        if (_brokerConfig.Auth == AuthMethod.X509 &&
            !string.IsNullOrEmpty(_brokerConfig.Cert))
        {
            var clientCert = CertificateHelper.LoadClientCertFromPem(
                _brokerConfig.Cert,
                _brokerConfig.Key);

            tlsOptions = new MqttClientTlsOptionsBuilder()
                .UseTls(true)
                .WithClientCertificates(new[] { clientCert })
                .Build();
        }

        optionsBuilder.WithTlsOptions(tlsOptions);

        var response = await _client.ConnectAsync(optionsBuilder.Build(), cancellationToken);
        _logger.LogInformation("Device {DeviceId} connected to IoT Hub {Hostname} (ResultCode: {ResultCode})",
            _deviceId, _hostname, response.ResultCode);
    }

    public async Task SendAsync(string payload, CancellationToken cancellationToken = default)
    {
        var message = new MqttApplicationMessageBuilder()
            .WithTopic(_topic)
            .WithPayload(payload)
            .WithQualityOfServiceLevel(MQTTnet.Protocol.MqttQualityOfServiceLevel.AtLeastOnce)
            .WithRetainFlag(false)
            .Build();

        await _client.PublishAsync(message, cancellationToken);
        _logger.LogDebug("Device {DeviceId} sent {Bytes} bytes to IoT Hub",
            _deviceId, payload.Length);
    }

    public async Task DisconnectAsync(CancellationToken cancellationToken = default)
    {
        if (_client.IsConnected)
        {
            await _client.DisconnectAsync(new MqttClientDisconnectOptionsBuilder().Build(), cancellationToken);
            _logger.LogInformation("Device {DeviceId} disconnected from IoT Hub", _deviceId);
        }
    }

    public async ValueTask DisposeAsync()
    {
        await DisconnectAsync();
        _client.Dispose();
    }

    private string GetPassword()
    {
        if (_brokerConfig.Auth == AuthMethod.X509)
            return string.Empty;

        return SasTokenGenerator.Generate(
            _hostname,
            _deviceId,
            _deviceKey,
            _sasTokenTtl);
    }
}
