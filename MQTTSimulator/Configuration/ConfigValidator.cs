using Microsoft.Extensions.Logging;

namespace MQTTSimulator.Configuration;

public static class ConfigValidator
{
    public static bool Validate(SimulatorConfig config, ILogger logger)
    {
        var errors = new List<string>();
        var warnings = new List<string>();

        ValidateProfiles(config.Profiles, errors);
        ValidateDevices(config.Devices, config.Profiles, errors, warnings);
        ValidateFleets(config.Fleets, config.Profiles, errors);

        if (config.Devices.Count == 0 && config.Fleets.Count == 0)
            errors.Add("No devices or fleets defined. Add at least one device or fleet.");

        foreach (var warning in warnings)
            logger.LogWarning("Config warning: {Warning}", warning);

        foreach (var error in errors)
            logger.LogError("Config error: {Error}", error);

        if (errors.Count > 0)
            logger.LogError("{Count} configuration error(s) found. Fix the issues above and restart.", errors.Count);

        return errors.Count == 0;
    }

    private static void ValidateProfiles(
        Dictionary<string, Dictionary<string, FieldConfig>> profiles,
        List<string> errors)
    {
        if (profiles.Count == 0)
        {
            errors.Add("No profiles defined. Add at least one telemetry profile.");
            return;
        }

        foreach (var (profileName, fields) in profiles)
        {
            if (fields.Count == 0)
            {
                errors.Add($"Profile '{profileName}': has no fields.");
                continue;
            }

            foreach (var (fieldName, field) in fields)
            {
                var prefix = $"Profile '{profileName}' → field '{fieldName}'";

                switch (field.Gen)
                {
                    case GeneratorType.Random:
                        if (field.Min >= field.Max)
                            errors.Add($"{prefix}: Random generator requires min < max (got min={field.Min}, max={field.Max}).");
                        break;

                    case GeneratorType.Increment:
                        if (field.Min >= field.Max)
                            errors.Add($"{prefix}: Increment generator requires min < max (got min={field.Min}, max={field.Max}).");
                        if (field.Step == 0)
                            errors.Add($"{prefix}: Increment generator requires non-zero step.");
                        break;

                    case GeneratorType.Sine:
                        if (field.Amplitude == 0)
                            errors.Add($"{prefix}: Sine generator requires non-zero amplitude.");
                        if (field.Period <= 0)
                            errors.Add($"{prefix}: Sine generator requires period > 0 (got {field.Period}).");
                        break;

                    case GeneratorType.Cycle:
                        if (field.Values.Count == 0)
                            errors.Add($"{prefix}: Cycle generator requires a non-empty values list.");
                        break;

                    case GeneratorType.HashSelect:
                        if (field.Values.Count == 0)
                            errors.Add($"{prefix}: HashSelect generator requires a non-empty values list.");
                        break;
                }
            }
        }
    }

    private static void ValidateDevices(
        List<DeviceConfig> devices,
        Dictionary<string, Dictionary<string, FieldConfig>> profiles,
        List<string> errors,
        List<string> warnings)
    {
        var deviceIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        for (int i = 0; i < devices.Count; i++)
        {
            var device = devices[i];
            var prefix = $"Device [{i}]";

            if (string.IsNullOrWhiteSpace(device.Id))
            {
                errors.Add($"{prefix}: id is required.");
            }
            else
            {
                prefix = $"Device '{device.Id}'";
                if (!deviceIds.Add(device.Id))
                    errors.Add($"{prefix}: duplicate device id.");
            }

            if (string.IsNullOrWhiteSpace(device.Profile))
                errors.Add($"{prefix}: profile is required.");
            else if (!profiles.ContainsKey(device.Profile))
                errors.Add($"{prefix}: references unknown profile '{device.Profile}'.");

            ValidateBroker(device.Broker, prefix, errors, warnings);
        }
    }

    private static void ValidateBroker(
        BrokerConfig broker,
        string prefix,
        List<string> errors,
        List<string> warnings)
    {
        switch (broker.Type)
        {
            case BrokerType.IoTHub:
                if (string.IsNullOrEmpty(broker.Connection) &&
                    (string.IsNullOrEmpty(broker.Host) || string.IsNullOrEmpty(broker.Cert) || string.IsNullOrEmpty(broker.Key)))
                    errors.Add($"{prefix}: IoTHub broker requires either 'connection' (SAS) or 'host' + 'cert' + 'key' (X509).");
                break;

            case BrokerType.EventHub:
                if (string.IsNullOrEmpty(broker.Connection))
                    errors.Add($"{prefix}: EventHub broker requires 'connection'.");
                break;

            case BrokerType.MqttMtls:
                if (string.IsNullOrEmpty(broker.Host))
                    errors.Add($"{prefix}: MqttMtls broker requires 'host'.");
                if (string.IsNullOrEmpty(broker.Cert))
                    errors.Add($"{prefix}: MqttMtls broker requires 'cert'.");
                if (string.IsNullOrEmpty(broker.Key))
                    errors.Add($"{prefix}: MqttMtls broker requires 'key'.");
                if (string.IsNullOrEmpty(broker.Ca))
                    errors.Add($"{prefix}: MqttMtls broker requires 'ca'.");
                if (string.IsNullOrEmpty(broker.Topic))
                    errors.Add($"{prefix}: MqttMtls broker requires 'topic'.");
                break;

            case BrokerType.Mqtt:
            case BrokerType.MqttTls:
                if (string.IsNullOrEmpty(broker.Host))
                    errors.Add($"{prefix}: {broker.Type} broker requires 'host'.");
                if (string.IsNullOrEmpty(broker.Topic))
                    errors.Add($"{prefix}: {broker.Type} broker requires 'topic'.");
                break;
        }

        // Warn if cert/key files don't exist on disk
        CheckFileExists(broker.Cert, $"{prefix} → cert", warnings);
        CheckFileExists(broker.Key, $"{prefix} → key", warnings);
        CheckFileExists(broker.Ca, $"{prefix} → ca", warnings);
    }

    private static void ValidateFleets(
        List<FleetConfig> fleets,
        Dictionary<string, Dictionary<string, FieldConfig>> profiles,
        List<string> errors)
    {
        for (int i = 0; i < fleets.Count; i++)
        {
            var fleet = fleets[i];
            var prefix = $"Fleet [{i}]";

            if (string.IsNullOrWhiteSpace(fleet.Prefix))
                errors.Add($"{prefix}: prefix is required.");
            else
                prefix = $"Fleet '{fleet.Prefix}'";

            if (fleet.Count <= 0)
                errors.Add($"{prefix}: count must be greater than 0 (got {fleet.Count}).");

            if (string.IsNullOrWhiteSpace(fleet.Profile))
                errors.Add($"{prefix}: profile is required.");
            else if (!profiles.ContainsKey(fleet.Profile))
                errors.Add($"{prefix}: references unknown profile '{fleet.Profile}'.");

            if (string.IsNullOrEmpty(fleet.Connection))
                errors.Add($"{prefix}: connection is required.");

            switch (fleet.Type)
            {
                case BrokerType.IoTHub:
                    break;

                case BrokerType.EventHub:
                    if (string.IsNullOrEmpty(fleet.Hub))
                        errors.Add($"{prefix}: EventHub fleet requires 'hub' (EventHub entity name).");
                    break;

                default:
                    errors.Add($"{prefix}: unsupported fleet type '{fleet.Type}'. Use IoTHub or EventHub.");
                    break;
            }
        }
    }

    private static void CheckFileExists(string path, string label, List<string> warnings)
    {
        if (!string.IsNullOrEmpty(path) && !File.Exists(path))
            warnings.Add($"{label}: file '{path}' not found.");
    }
}
