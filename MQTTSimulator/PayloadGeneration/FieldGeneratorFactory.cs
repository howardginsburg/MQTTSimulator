using MQTTSimulator.Configuration;

namespace MQTTSimulator.PayloadGeneration;

public static class FieldGeneratorFactory
{
    public static IFieldGenerator Create(FieldConfig config, string deviceId)
    {
        return config.Generator switch
        {
            GeneratorType.Increment => new IncrementGenerator(config),
            GeneratorType.RandomRange => new RandomRangeGenerator(config),
            GeneratorType.SineWave => new SineWaveGenerator(config),
            GeneratorType.Static => new StaticGenerator(config),
            GeneratorType.EnumCycle => new EnumCycleGenerator(config),
            GeneratorType.Timestamp => new TimestampGenerator(config.Name),
            GeneratorType.DeviceHashSelect => new DeviceHashSelectGenerator(config, deviceId),
            _ => throw new ArgumentException($"Unknown generator type: {config.Generator}")
        };
    }
}
