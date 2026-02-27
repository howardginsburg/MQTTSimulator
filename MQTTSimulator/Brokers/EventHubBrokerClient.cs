using Azure.Messaging.EventHubs;
using Azure.Messaging.EventHubs.Producer;
using Microsoft.Extensions.Logging;
using MQTTSimulator.Configuration;
using System.Text;

namespace MQTTSimulator.Brokers;

public class EventHubBrokerClient : IBrokerClient
{
    private readonly string _deviceId;
    private readonly BrokerConfig _brokerConfig;
    private readonly ILogger _logger;
    private EventHubProducerClient? _producer;

    public EventHubBrokerClient(DeviceConfig deviceConfig, ILogger logger)
    {
        _deviceId = deviceConfig.Id;
        _brokerConfig = deviceConfig.Broker;
        _logger = logger;
    }

    public Task ConnectAsync(CancellationToken cancellationToken = default)
    {
        var connection = _brokerConfig.Connection;
        var hub = _brokerConfig.Hub;

        _producer = string.IsNullOrEmpty(hub)
            ? new EventHubProducerClient(connection)
            : new EventHubProducerClient(connection, hub);

        _logger.LogInformation("Device {DeviceId} connected to EventHub {HubName}",
            _deviceId, _producer.EventHubName);
        return Task.CompletedTask;
    }

    public async Task SendAsync(string payload, CancellationToken cancellationToken = default)
    {
        var eventData = new EventData(Encoding.UTF8.GetBytes(payload));
        eventData.Properties["deviceId"] = _deviceId;

        // Use device ID as partition key to keep per-device ordering
        var options = new SendEventOptions { PartitionKey = _deviceId };

        using var batch = await _producer!.CreateBatchAsync(
            new CreateBatchOptions { PartitionKey = _deviceId }, cancellationToken);

        if (!batch.TryAdd(eventData))
            throw new InvalidOperationException($"Payload too large for a single Event Hub batch ({payload.Length} bytes)");

        await _producer.SendAsync(batch, cancellationToken);

        _logger.LogDebug("Device {DeviceId} sent {Bytes} bytes to EventHub {HubName}",
            _deviceId, payload.Length, _producer.EventHubName);
    }

    public Task DisconnectAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Device {DeviceId} disconnected from EventHub", _deviceId);
        return Task.CompletedTask;
    }

    public async ValueTask DisposeAsync()
    {
        if (_producer is not null)
            await _producer.DisposeAsync();
    }
}
