using MQTTSimulator.Configuration;

namespace MQTTSimulator.PayloadGeneration;

public class IncrementGenerator : IFieldGenerator
{
    private double _currentValue;
    private int _direction = 1;
    private readonly double _step;
    private readonly double _min;
    private readonly double _max;

    public string FieldName { get; }

    public IncrementGenerator(string fieldName, FieldConfig config)
    {
        FieldName = fieldName;
        _currentValue = config.Init;
        _step = config.Step;
        _min = config.Min;
        _max = config.Max;
    }

    public object GenerateNext()
    {
        var value = _currentValue;
        _currentValue += _step * _direction;

        if (_currentValue >= _max)
        {
            _currentValue = _max;
            _direction = -1;
        }
        else if (_currentValue <= _min)
        {
            _currentValue = _min;
            _direction = 1;
        }

        return value;
    }
}
