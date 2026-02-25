using System.Text.Json;
using Microsoft.Extensions.Logging;
using MQTTSimulator.Brokers;
using MQTTSimulator.Configuration;
using MQTTSimulator.PayloadGeneration;

namespace MQTTSimulator.Simulation;

public class DeviceSimulator : IAsyncDisposable
{
    private readonly DeviceConfig _deviceConfig;
    private readonly IBrokerClient _brokerClient;
    private readonly List<IFieldGenerator> _generators;
    private readonly ILogger _logger;
    private readonly ConsoleDisplay _display;
    private long _messageId;

    public DeviceSimulator(DeviceConfig deviceConfig, List<FieldConfig> fieldConfigs, ILogger logger, ConsoleDisplay display)
    {
        _deviceConfig = deviceConfig;
        _logger = logger;
        _display = display;
        _brokerClient = BrokerClientFactory.Create(deviceConfig, logger);
        _generators = fieldConfigs.Select(f => FieldGeneratorFactory.Create(f, deviceConfig.DeviceId)).ToList();
    }

    public async Task RunAsync(CancellationToken cancellationToken)
    {
        try
        {
            await _brokerClient.ConnectAsync(cancellationToken);
            _display.UpdateStatus(_deviceConfig.DeviceId, "Connected");

            while (!cancellationToken.IsCancellationRequested)
            {
                var payload = BuildPayload();
                _logger.LogInformation("Device {DeviceId} sending telemetry: {Payload}", _deviceConfig.DeviceId, payload);
                await _brokerClient.SendAsync(payload, cancellationToken);
                _display.RecordTelemetry(_deviceConfig.DeviceId, _messageId, payload);
                await Task.Delay(_deviceConfig.SendIntervalMs, cancellationToken);
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Device {DeviceId} simulation stopped", _deviceConfig.DeviceId);
            _display.UpdateStatus(_deviceConfig.DeviceId, "Stopped");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Device {DeviceId} encountered an error", _deviceConfig.DeviceId);
            _display.RecordError(_deviceConfig.DeviceId, ex.Message);
        }
        finally
        {
            await _brokerClient.DisconnectAsync();
        }
    }

    private string BuildPayload()
    {
        var data = new Dictionary<string, object>
        {
            ["messageId"] = ++_messageId,
            ["deviceId"] = _deviceConfig.DeviceId
        };

        foreach (var generator in _generators)
        {
            data[generator.FieldName] = generator.GenerateNext();
        }

        return JsonSerializer.Serialize(data, new JsonSerializerOptions
        {
            WriteIndented = false,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });
    }

    public async ValueTask DisposeAsync()
    {
        await _brokerClient.DisposeAsync();
    }
}
