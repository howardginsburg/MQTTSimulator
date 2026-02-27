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

        // Resolve named broker references before validation so the validator sees the full config
        if (!ResolveBrokerRefs())
            throw new InvalidOperationException("Broker reference resolution failed. See log output for details.");

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

            _logger.LogInformation("Started device {DeviceId} with profile '{ProfileName}' (interval: {Interval})",
                deviceConfig.Id, deviceConfig.Profile, deviceConfig.Interval ?? deviceConfig.EffectiveInterval);
        }

        _displayTask = _display.RunAsync(_cts.Token);
    }

    private bool ResolveBrokerRefs()
    {
        var ok = true;

        foreach (var device in _config.Devices)
        {
            if (string.IsNullOrEmpty(device.Broker.Name)) continue;

            if (_config.Brokers.TryGetValue(device.Broker.Name, out var named))
                device.Broker = MergeBroker(named, device.Broker);
            else
            {
                _logger.LogError("Device '{Id}' references unknown broker '{Ref}'. Available brokers: [{List}]",
                    device.Id, device.Broker.Name, string.Join(", ", _config.Brokers.Keys));
                ok = false;
            }
        }

        foreach (var fleet in _config.Fleets)
        {
            if (string.IsNullOrEmpty(fleet.BrokerRef)) continue;

            if (_config.Brokers.TryGetValue(fleet.BrokerRef, out var named))
            {
                fleet.Type = named.Type;
                fleet.Connection = named.Connection;
                fleet.Hub = named.Hub;
            }
            else
            {
                _logger.LogError("Fleet '{Prefix}' references unknown broker '{Ref}'. Available brokers: [{List}]",
                    fleet.Prefix, fleet.BrokerRef, string.Join(", ", _config.Brokers.Keys));
                ok = false;
            }
        }

        return ok;
    }

    /// <summary>
    /// Produces a broker config that starts from <paramref name="named"/> and applies
    /// any non-empty/non-default fields from <paramref name="overrides"/> on top.
    /// This lets devices share a common named broker while overriding only cert/key/topic/etc.
    /// </summary>
    private static BrokerConfig MergeBroker(BrokerConfig named, BrokerConfig overrides) => new()
    {
        Type       = overrides.Type != BrokerConfig.DefaultType ? overrides.Type : named.Type,
        Host       = !string.IsNullOrEmpty(overrides.Host)       ? overrides.Host       : named.Host,
        Port       = overrides.Port > 0                          ? overrides.Port       : named.Port,
        Topic      = !string.IsNullOrEmpty(overrides.Topic)      ? overrides.Topic      : named.Topic,
        Connection = !string.IsNullOrEmpty(overrides.Connection) ? overrides.Connection : named.Connection,
        User       = !string.IsNullOrEmpty(overrides.User)       ? overrides.User       : named.User,
        Pass       = !string.IsNullOrEmpty(overrides.Pass)       ? overrides.Pass       : named.Pass,
        Cert       = !string.IsNullOrEmpty(overrides.Cert)       ? overrides.Cert       : named.Cert,
        Key        = !string.IsNullOrEmpty(overrides.Key)        ? overrides.Key        : named.Key,
        Ca         = !string.IsNullOrEmpty(overrides.Ca)         ? overrides.Ca         : named.Ca,
        Hub        = !string.IsNullOrEmpty(overrides.Hub)        ? overrides.Hub        : named.Hub,
    };

    private static BrokerConfig CloneBroker(BrokerConfig b) => new()
    {
        Type = b.Type,
        Host = b.Host,
        Port = b.Port,
        Topic = b.Topic,
        Connection = b.Connection,
        User = b.User,
        Pass = b.Pass,
        Cert = b.Cert,
        Key = b.Key,
        Ca = b.Ca,
        Hub = b.Hub,
    };

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
