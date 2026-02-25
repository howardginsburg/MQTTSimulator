using System.Security.Cryptography;
using System.Text;
using System.Web;
using Microsoft.Extensions.Logging;
using MQTTSimulator.Configuration;

namespace MQTTSimulator.Brokers;

public class EventHubBrokerClient : IBrokerClient
{
    private readonly string _deviceId;
    private readonly BrokerConfig _brokerConfig;
    private readonly ILogger _logger;
    private readonly HttpClient _httpClient = new();
    private string _namespace = string.Empty;
    private string _hubName = string.Empty;
    private string _keyName = string.Empty;
    private string _key = string.Empty;

    public EventHubBrokerClient(DeviceConfig deviceConfig, ILogger logger)
    {
        _deviceId = deviceConfig.Id;
        _brokerConfig = deviceConfig.Broker;
        _logger = logger;
    }

    public Task ConnectAsync(CancellationToken cancellationToken = default)
    {
        ParseConnectionString(_brokerConfig.Connection);

        if (!string.IsNullOrEmpty(_brokerConfig.Hub))
            _hubName = _brokerConfig.Hub;

        _logger.LogInformation("Device {DeviceId} connected to EventHub {HubName} on {Namespace}",
            _deviceId, _hubName, _namespace);
        return Task.CompletedTask;
    }

    public async Task SendAsync(string payload, CancellationToken cancellationToken = default)
    {
        var uri = $"https://{_namespace}.servicebus.windows.net/{_hubName}/messages";
        var sasToken = GenerateSasToken($"{_namespace}.servicebus.windows.net/{_hubName}");

        using var request = new HttpRequestMessage(HttpMethod.Post, uri);
        request.Headers.Add("Authorization", sasToken);
        request.Headers.Add("BrokerProperties", $"{{\"PartitionKey\":\"{_deviceId}\"}}");
        request.Content = new StringContent(payload, Encoding.UTF8, "application/json");

        var response = await _httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();

        _logger.LogDebug("Device {DeviceId} sent {Bytes} bytes to EventHub {HubName}",
            _deviceId, payload.Length, _hubName);
    }

    public Task DisconnectAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Device {DeviceId} disconnected from EventHub", _deviceId);
        return Task.CompletedTask;
    }

    public ValueTask DisposeAsync()
    {
        _httpClient.Dispose();
        return ValueTask.CompletedTask;
    }

    private void ParseConnectionString(string connectionString)
    {
        foreach (var part in connectionString.Split(';'))
        {
            var idx = part.IndexOf('=');
            if (idx < 0) continue;
            var key = part[..idx].Trim();
            var value = part[(idx + 1)..].Trim();
            switch (key)
            {
                case "Endpoint":
                    _namespace = new Uri(value).Host.Split('.')[0];
                    break;
                case "SharedAccessKeyName":
                    _keyName = value;
                    break;
                case "SharedAccessKey":
                    _key = value;
                    break;
                case "EntityPath":
                    _hubName = value;
                    break;
            }
        }
    }

    private string GenerateSasToken(string resourceUri)
    {
        var encodedUri = HttpUtility.UrlEncode(resourceUri);
        var expiry = DateTimeOffset.UtcNow.Add(TimeSpan.FromHours(1)).ToUnixTimeSeconds();
        var stringToSign = $"{encodedUri}\n{expiry}";

        using var hmac = new HMACSHA256(Convert.FromBase64String(_key));
        var signature = Convert.ToBase64String(hmac.ComputeHash(Encoding.UTF8.GetBytes(stringToSign)));
        return $"SharedAccessSignature sr={encodedUri}&sig={HttpUtility.UrlEncode(signature)}&se={expiry}&skn={_keyName}";
    }
}
