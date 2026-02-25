using System.Text.Json.Serialization;

namespace MQTTSimulator.Configuration;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum GeneratorType
{
    Increment,
    RandomRange,
    SineWave,
    Static,
    EnumCycle,
    Timestamp,
    DeviceHashSelect
}

public class FieldConfig
{
    public string Name { get; set; } = string.Empty;
    public string DataType { get; set; } = "double";
    public GeneratorType Generator { get; set; } = GeneratorType.Static;

    // Increment generator
    public double InitialValue { get; set; }
    public double Step { get; set; } = 1.0;

    // Shared by Increment, RandomRange
    public double Min { get; set; }
    public double Max { get; set; } = 100.0;

    // SineWave generator
    public double Amplitude { get; set; } = 1.0;
    public double Offset { get; set; }
    public int PeriodSeconds { get; set; } = 60;

    // Static generator
    public string Value { get; set; } = string.Empty;

    // EnumCycle generator
    public List<string> Values { get; set; } = new();
}
