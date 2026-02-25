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

        // Validate configuration before doing anything
        if (!ConfigValidator.Validate(_config, _logger))
            throw new InvalidOperationException("Configuration validation failed. See log output for details.");

        // Provision fleet devices from IoT Hub owner connection strings
        var allDevices = new List<DeviceConfig>(_config.Devices);

        // Apply default interval to devices/fleets that don't override
        foreach (var d in allDevices)
            d.EffectiveInterval = _config.DefaultInterval;

        foreach (var fleet in _config.Fleets.Where(f => f.Enabled))
        {
            fleet.EffectiveInterval = _config.DefaultInterval;
            _logger.LogInformation("Provisioning {Count} device(s) with prefix '{Prefix}' ({Type})",
                fleet.Count, fleet.Prefix, fleet.Type);

            List<DeviceConfig> fleetDevices;

            if (fleet.Type == BrokerType.EventHub)
            {
                fleetDevices = new List<DeviceConfig>();
                for (int i = 1; i <= fleet.Count; i++)
                {
                    fleetDevices.Add(new DeviceConfig
                    {
                        Id = $"{fleet.Prefix}-{i:D3}",
                        Enabled = true,
                        Interval = fleet.Interval,
                        EffectiveInterval = fleet.EffectiveInterval,
                        Profile = fleet.Profile,
                        Broker = new BrokerConfig
                        {
                            Type = BrokerType.EventHub,
                            Connection = fleet.Connection,
                            Hub = fleet.Hub
                        }
                    });
                }
            }
            else
            {
                var manager = new IoTHubDeviceManager(fleet.Connection, _logger);
                fleetDevices = await manager.ProvisionDevicesAsync(fleet, cancellationToken);
            }

            allDevices.AddRange(fleetDevices);
        }

        var enabledDevices = allDevices.Where(d => d.Enabled).ToList();
        _logger.LogInformation("Starting simulation for {Count} device(s)", enabledDevices.Count);

        for (int i = 0; i < enabledDevices.Count; i++)
        {
            var deviceConfig = enabledDevices[i];

            if (!_config.Profiles.TryGetValue(deviceConfig.Profile, out var fieldConfigs))
            {
                _logger.LogError("Device {DeviceId} references unknown telemetry profile '{ProfileName}'. Skipping.",
                    deviceConfig.Id, deviceConfig.Profile);
                continue;
            }

            _display.RegisterDevice(deviceConfig.Id, deviceConfig.Broker.Type.ToString());

            if (_config.ConnectionDelayMs > 0 && i > 0)
            {
                await Task.Delay(i * _config.ConnectionDelayMs, cancellationToken);
            }

            var deviceLogger = _loggerFactory.CreateLogger($"Device.{deviceConfig.Id}");
            var simulator = new DeviceSimulator(deviceConfig, fieldConfigs, deviceLogger, _display);
            _simulators.Add(simulator);
            _runningTasks.Add(simulator.RunAsync(_cts.Token));

            _logger.LogInformation("Started device {DeviceId} with profile '{ProfileName}' (interval: {IntervalMs}ms)",
                deviceConfig.Id, deviceConfig.Profile, deviceConfig.IntervalMs);
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
