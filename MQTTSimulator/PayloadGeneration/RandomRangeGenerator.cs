using MQTTSimulator.Configuration;

namespace MQTTSimulator.PayloadGeneration;

public class RandomRangeGenerator : IFieldGenerator
{
    private readonly Random _random = new();
    private readonly double _min;
    private readonly double _max;

    public string FieldName { get; }

    public RandomRangeGenerator(string fieldName, FieldConfig config)
    {
        FieldName = fieldName;
        _min = config.Min;
        _max = config.Max;
    }

    public object GenerateNext()
    {
        return Math.Round(_min + _random.NextDouble() * (_max - _min), 2);
    }
}
