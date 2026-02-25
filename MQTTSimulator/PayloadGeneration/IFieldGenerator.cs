namespace MQTTSimulator.PayloadGeneration;

public interface IFieldGenerator
{
    string FieldName { get; }
    object GenerateNext();
}
