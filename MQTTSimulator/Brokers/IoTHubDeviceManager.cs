using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Web;
using Microsoft.Extensions.Logging;
using MQTTSimulator.Configuration;

namespace MQTTSimulator.Brokers;

public class IoTHubDeviceManager
{
    private readonly string _hostname;
    private readonly string _sharedAccessKeyName;
    private readonly string _sharedAccessKey;
    private readonly HttpClient _httpClient;
    private readonly ILogger _logger;
    private const string ApiVersion = "2021-04-12";

    public IoTHubDeviceManager(string ownerConnectionString, ILogger logger)
    {
        _logger = logger;
        _httpClient = new HttpClient();

        var parts = ownerConnectionString.Split(';')
            .Select(part => part.Split('=', 2))
            .Where(kv => kv.Length == 2)
            .ToDictionary(kv => kv[0].Trim(), kv => kv[1].Trim(), StringComparer.OrdinalIgnoreCase);

        if (!parts.TryGetValue("HostName", out var hostname))
            throw new ArgumentException("Owner connection string missing 'HostName'");
        if (!parts.TryGetValue("SharedAccessKeyName", out var keyName))
            throw new ArgumentException("Owner connection string missing 'SharedAccessKeyName'");
        if (!parts.TryGetValue("SharedAccessKey", out var key))
            throw new ArgumentException("Owner connection string missing 'SharedAccessKey'");

        _hostname = hostname;
        _sharedAccessKeyName = keyName;
        _sharedAccessKey = key;
    }

    public async Task<List<DeviceConfig>> ProvisionDevicesAsync(IoTHubFleetConfig fleetConfig, CancellationToken cancellationToken)
    {
        var devices = new List<DeviceConfig>();

        for (int i = 1; i <= fleetConfig.DeviceCount; i++)
        {
            var deviceId = $"{fleetConfig.DevicePrefix}-{i:D3}";
            var deviceKey = await EnsureDeviceAsync(deviceId, cancellationToken);

            var deviceConnectionString = $"HostName={_hostname};DeviceId={deviceId};SharedAccessKey={deviceKey}";

            devices.Add(new DeviceConfig
            {
                DeviceId = deviceId,
                Enabled = true,
                SendIntervalMs = fleetConfig.SendIntervalMs,
                TelemetryProfileName = fleetConfig.TelemetryProfileName,
                Broker = new BrokerConfig
                {
                    Type = BrokerType.IoTHub,
                    AuthMethod = AuthMethod.SAS,
                    ConnectionString = deviceConnectionString
                }
            });

            _logger.LogInformation("Provisioned device {DeviceId}", deviceId);
        }

        return devices;
    }

    private async Task<string> EnsureDeviceAsync(string deviceId, CancellationToken cancellationToken)
    {
        var sasToken = GenerateServiceSasToken(TimeSpan.FromMinutes(5));

        // Try GET first
        var getUrl = $"https://{_hostname}/devices/{Uri.EscapeDataString(deviceId)}?api-version={ApiVersion}";
        using var getRequest = new HttpRequestMessage(HttpMethod.Get, getUrl);
        getRequest.Headers.Authorization = AuthenticationHeaderValue.Parse(sasToken);

        var getResponse = await _httpClient.SendAsync(getRequest, cancellationToken);

        if (getResponse.StatusCode == HttpStatusCode.OK)
        {
            var body = await getResponse.Content.ReadAsStringAsync(cancellationToken);
            var deviceKey = ExtractPrimaryKey(body);
            _logger.LogInformation("Device {DeviceId} already exists", deviceId);
            return deviceKey;
        }

        // Device doesn't exist — create it
        var putUrl = $"https://{_hostname}/devices/{Uri.EscapeDataString(deviceId)}?api-version={ApiVersion}";
        var deviceBody = JsonSerializer.Serialize(new { deviceId });

        using var putRequest = new HttpRequestMessage(HttpMethod.Put, putUrl);
        putRequest.Headers.Authorization = AuthenticationHeaderValue.Parse(sasToken);
        putRequest.Content = new StringContent(deviceBody, Encoding.UTF8, "application/json");

        var putResponse = await _httpClient.SendAsync(putRequest, cancellationToken);
        putResponse.EnsureSuccessStatusCode();

        var createBody = await putResponse.Content.ReadAsStringAsync(cancellationToken);
        _logger.LogInformation("Created device {DeviceId} in IoT Hub", deviceId);
        return ExtractPrimaryKey(createBody);
    }

    private static string ExtractPrimaryKey(string responseBody)
    {
        using var doc = JsonDocument.Parse(responseBody);
        return doc.RootElement
            .GetProperty("authentication")
            .GetProperty("symmetricKey")
            .GetProperty("primaryKey")
            .GetString() ?? throw new InvalidOperationException("Could not extract primary key from device response");
    }

    private string GenerateServiceSasToken(TimeSpan ttl)
    {
        var encodedResourceUri = HttpUtility.UrlEncode(_hostname);
        var expiry = DateTimeOffset.UtcNow.Add(ttl).ToUnixTimeSeconds();
        var stringToSign = $"{encodedResourceUri}\n{expiry}";

        using var hmac = new HMACSHA256(Convert.FromBase64String(_sharedAccessKey));
        var signature = Convert.ToBase64String(hmac.ComputeHash(Encoding.UTF8.GetBytes(stringToSign)));
        var encodedSignature = HttpUtility.UrlEncode(signature);

        return $"SharedAccessSignature sr={encodedResourceUri}&sig={encodedSignature}&se={expiry}&skn={_sharedAccessKeyName}";
    }
}
