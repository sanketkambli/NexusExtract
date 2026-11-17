namespace NexusExtract.Domain.Interfaces;

public interface IMessageBus
{
    Task PublishAsync<T>(string topic, T message, string correlationId, CancellationToken ct = default) where T : class;

    Task SubscribeAsync<T>(string topic, Func<T, string, Task> handler, CancellationToken ct = default) where T : class;
}
