using MQTTSimulator.Configuration;

namespace MQTTSimulator.PayloadGeneration;

public class StaticGenerator : IFieldGenerator
{
    private readonly object _value;

    public string FieldName { get; }

    public StaticGenerator(FieldConfig config)
    {
        FieldName = config.Name;
        _value = config.DataType.ToLowerInvariant() switch
        {
            "double" => config.InitialValue,
            "int" => (int)config.InitialValue,
            "bool" => bool.TryParse(config.Value, out var b) && b,
            _ => config.Value
        };
    }

    public object GenerateNext() => _value;
}
