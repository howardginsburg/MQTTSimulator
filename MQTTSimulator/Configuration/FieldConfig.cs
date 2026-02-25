namespace MQTTSimulator.Configuration;

public enum GeneratorType
{
    Increment,
    Random,
    Sine,
    Static,
    Cycle,
    Timestamp,
    HashSelect
}

public class FieldConfig
{
    public GeneratorType Gen { get; set; } = GeneratorType.Static;

    // Increment
    public double Init { get; set; }
    public double Step { get; set; } = 1.0;

    // Increment, Random
    public double Min { get; set; }
    public double Max { get; set; } = 100.0;

    // Sine
    public double Amplitude { get; set; } = 1.0;
    public double Offset { get; set; }
    public int Period { get; set; } = 60;

    // Static
    public string Value { get; set; } = string.Empty;

    // Cycle, HashSelect
    public List<string> Values { get; set; } = new();
}
