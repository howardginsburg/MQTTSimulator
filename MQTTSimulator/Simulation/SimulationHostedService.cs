using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MQTTSimulator.Brokers;
using MQTTSimulator.Configuration;

namespace MQTTSimulator.Simulation;

public class SimulationHostedService : IHostedService
{
    private readonly SimulatorConfig _config;
    private readonly ILogger<SimulationHostedService> _logger;
    private readonly ILoggerFactory _loggerFactory;
    private readonly ConsoleDisplay _display;
    private readonly List<DeviceSimulator> _simulators = new();
    private readonly List<Task> _runningTasks = new();
    private CancellationTokenSource? _cts;
    private Task? _displayTask;

    public SimulationHostedService(
        IOptions<SimulatorConfig> config,
        ILogger<SimulationHostedService> logger,
        ILoggerFactory loggerFactory,
        ConsoleDisplay display)
    {
        _config = config.Value;
        _logger = logger;
        _loggerFactory = loggerFactory;
        _display = display;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        // Provision fleet devices from IoT Hub owner connection strings
        var allDevices = new List<DeviceConfig>(_config.Devices);

        foreach (var fleet in _config.IoTHubFleets.Where(f => f.Enabled))
        {
            _logger.LogInformation("Provisioning {Count} device(s) with prefix '{Prefix}' in IoT Hub",
                fleet.DeviceCount, fleet.DevicePrefix);
            var manager = new IoTHubDeviceManager(fleet.ConnectionString, _logger);
            var fleetDevices = await manager.ProvisionDevicesAsync(fleet, cancellationToken);
            allDevices.AddRange(fleetDevices);
        }

        var enabledDevices = allDevices.Where(d => d.Enabled).ToList();
        _logger.LogInformation("Starting simulation for {Count} device(s)", enabledDevices.Count);

        for (int i = 0; i < enabledDevices.Count; i++)
        {
            var deviceConfig = enabledDevices[i];

            if (!_config.TelemetryProfiles.TryGetValue(deviceConfig.TelemetryProfileName, out var fieldConfigs))
            {
                _logger.LogError("Device {DeviceId} references unknown telemetry profile '{ProfileName}'. Skipping.",
                    deviceConfig.DeviceId, deviceConfig.TelemetryProfileName);
                continue;
            }

            _display.RegisterDevice(deviceConfig.DeviceId, deviceConfig.Broker.Type.ToString());

            if (_config.ConnectionDelayMs > 0 && i > 0)
            {
                await Task.Delay(i * _config.ConnectionDelayMs, cancellationToken);
            }

            var deviceLogger = _loggerFactory.CreateLogger($"Device.{deviceConfig.DeviceId}");
            var simulator = new DeviceSimulator(deviceConfig, fieldConfigs, deviceLogger, _display);
            _simulators.Add(simulator);
            _runningTasks.Add(simulator.RunAsync(_cts.Token));

            _logger.LogInformation("Started device {DeviceId} with profile '{ProfileName}' (interval: {IntervalMs}ms)",
                deviceConfig.DeviceId, deviceConfig.TelemetryProfileName, deviceConfig.SendIntervalMs);
        }

        _displayTask = _display.RunAsync(_cts.Token);
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Stopping simulation...");

        if (_cts is not null)
        {
            await _cts.CancelAsync();
        }

        if (_runningTasks.Count > 0)
        {
            await Task.WhenAll(_runningTasks);
        }

        if (_displayTask is not null)
        {
            await _displayTask;
        }

        foreach (var simulator in _simulators)
        {
            await simulator.DisposeAsync();
        }

        _simulators.Clear();
        _runningTasks.Clear();
        _cts?.Dispose();

        _logger.LogInformation("Simulation stopped");
    }
}
