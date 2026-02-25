using MQTTSimulator.Configuration;

namespace MQTTSimulator.PayloadGeneration;

public class EnumCycleGenerator : IFieldGenerator
{
    private readonly List<string> _values;
    private int _index;

    public string FieldName { get; }

    public EnumCycleGenerator(FieldConfig config)
    {
        FieldName = config.Name;
        _values = config.Values;
        _index = 0;
    }

    public object GenerateNext()
    {
        var value = _values[_index];
        _index = (_index + 1) % _values.Count;
        return value;
    }
}
