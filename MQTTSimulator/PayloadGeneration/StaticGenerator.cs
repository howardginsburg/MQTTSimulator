using MQTTSimulator.Configuration;

namespace MQTTSimulator.PayloadGeneration;

public class StaticGenerator : IFieldGenerator
{
    private readonly object _value;

    public string FieldName { get; }

    public StaticGenerator(string fieldName, FieldConfig config)
    {
        FieldName = fieldName;
        _value = config.Value switch
        {
            "" when config.Init != 0 => config.Init,
            "" => 0.0,
            var v when double.TryParse(v, out var d) => d,
            var v when bool.TryParse(v, out var b) => b,
            var v => v
        };
    }

    public object GenerateNext() => _value;
}
