namespace MQTTSimulator.Brokers;

public interface IBrokerClient : IAsyncDisposable
{
    Task ConnectAsync(CancellationToken cancellationToken = default);
    Task SendAsync(string payload, CancellationToken cancellationToken = default);
    Task DisconnectAsync(CancellationToken cancellationToken = default);
}
