using MQTTSimulator.Configuration;

namespace MQTTSimulator.PayloadGeneration;

public class DeviceHashSelectGenerator : IFieldGenerator
{
    private readonly string _selectedValue;

    public string FieldName { get; }

    public DeviceHashSelectGenerator(FieldConfig config, string deviceId)
    {
        FieldName = config.Name;
        var hash = Math.Abs(deviceId.GetHashCode(StringComparison.Ordinal));
        var index = hash % config.Values.Count;
        _selectedValue = config.Values[index];
    }

    public object GenerateNext() => _selectedValue;
}
