using Azure.Messaging.EventHubs;
using Azure.Messaging.EventHubs.Producer;
using Microsoft.Extensions.Logging;
using MQTTSimulator.Configuration;

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
        _producer = string.IsNullOrEmpty(_brokerConfig.Hub)
            ? new EventHubProducerClient(_brokerConfig.Connection)
            : new EventHubProducerClient(_brokerConfig.Connection, _brokerConfig.Hub);

        _logger.LogInformation("Device {DeviceId} connected to EventHub", _deviceId);
        return Task.CompletedTask;
    }

    public async Task SendAsync(string payload, CancellationToken cancellationToken = default)
    {
        if (_producer is null)
            throw new InvalidOperationException("Not connected. Call ConnectAsync first.");

        var batchOptions = new CreateBatchOptions { PartitionKey = _deviceId };
        using var batch = await _producer.CreateBatchAsync(batchOptions, cancellationToken);

        var eventData = new EventData(payload);
        if (!batch.TryAdd(eventData))
            throw new InvalidOperationException($"Payload too large for EventHub batch ({payload.Length} bytes)");

        await _producer.SendAsync(batch, cancellationToken);
        _logger.LogDebug("Device {DeviceId} sent {Bytes} bytes to EventHub", _deviceId, payload.Length);
    }

    public async Task DisconnectAsync(CancellationToken cancellationToken = default)
    {
        if (_producer is not null)
        {
            await _producer.CloseAsync(cancellationToken);
            _logger.LogInformation("Device {DeviceId} disconnected from EventHub", _deviceId);
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_producer is not null)
        {
            await _producer.DisposeAsync();
            _producer = null;
        }
    }
}
