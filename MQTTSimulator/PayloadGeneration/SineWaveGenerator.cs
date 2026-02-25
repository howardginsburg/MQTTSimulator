using MQTTSimulator.Configuration;

namespace MQTTSimulator.PayloadGeneration;

public class SineWaveGenerator : IFieldGenerator
{
    private readonly double _amplitude;
    private readonly double _offset;
    private readonly double _periodSeconds;
    private readonly DateTime _startTime;

    public string FieldName { get; }

    public SineWaveGenerator(string fieldName, FieldConfig config)
    {
        FieldName = fieldName;
        _amplitude = config.Amplitude;
        _offset = config.Offset;
        _periodSeconds = config.Period;
        _startTime = DateTime.UtcNow;
    }

    public object GenerateNext()
    {
        var elapsed = (DateTime.UtcNow - _startTime).TotalSeconds;
        var value = _offset + _amplitude * Math.Sin(2.0 * Math.PI * elapsed / _periodSeconds);
        return Math.Round(value, 2);
    }
}
