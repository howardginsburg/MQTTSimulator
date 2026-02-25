using MQTTSimulator.Configuration;

namespace MQTTSimulator.PayloadGeneration;

public static class FieldGeneratorFactory
{
    public static IFieldGenerator Create(string fieldName, FieldConfig config, string deviceId)
    {
        return config.Gen switch
        {
            GeneratorType.Increment => new IncrementGenerator(fieldName, config),
            GeneratorType.Random => new RandomRangeGenerator(fieldName, config),
            GeneratorType.Sine => new SineWaveGenerator(fieldName, config),
            GeneratorType.Static => new StaticGenerator(fieldName, config),
            GeneratorType.Cycle => new EnumCycleGenerator(fieldName, config),
            GeneratorType.Timestamp => new TimestampGenerator(fieldName),
            GeneratorType.HashSelect => new DeviceHashSelectGenerator(fieldName, config, deviceId),
            _ => throw new ArgumentException($"Unknown generator type: {config.Gen}")
        };
    }
}
