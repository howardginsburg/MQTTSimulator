namespace MQTTSimulator.PayloadGeneration;

public class TimestampGenerator : IFieldGenerator
{
    public string FieldName { get; }

    public TimestampGenerator(string fieldName)
    {
        FieldName = fieldName;
    }

    public object GenerateNext() => DateTime.UtcNow.ToString("o");
}
