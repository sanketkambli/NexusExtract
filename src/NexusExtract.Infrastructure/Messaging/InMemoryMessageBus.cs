using System.Collections.Concurrent;
using System.Text.Json;
using System.Threading.Channels;
using Microsoft.Extensions.Logging;
using NexusExtract.Domain.Interfaces;

namespace NexusExtract.Infrastructure.Messaging;

public sealed class InMemoryMessageBus : IMessageBus
{
    private readonly ConcurrentDictionary<string, List<Func<object, string, Task>>> _subscribers = new();
    private readonly Channel<(string Topic, string MessageJson, string CorrelationId, Type TargetType)> _channel = 
        Channel.CreateUnbounded<(string Topic, string MessageJson, string CorrelationId, Type TargetType)>(
            new UnboundedChannelOptions { SingleReader = false, SingleWriter = false });
    private readonly ILogger<InMemoryMessageBus> _logger;

    public InMemoryMessageBus(ILogger<InMemoryMessageBus> logger)
    {
        _logger = logger;
        Task.Run(ProcessQueueAsync);
    }

    public Task PublishAsync<T>(string topic, T message, string correlationId, CancellationToken ct = default) where T : class
    {
        var json = JsonSerializer.Serialize(message);
        _channel.Writer.TryWrite((topic, json, correlationId, typeof(T)));
        _logger.LogDebug("[InMemoryBus] Published event to topic {Topic} with Correlation {Corr}", topic, correlationId);
        return Task.CompletedTask;
    }

    public Task SubscribeAsync<T>(string topic, Func<T, string, Task> handler, CancellationToken ct = default) where T : class
    {
        var handlers = _subscribers.GetOrAdd(topic, _ => new List<Func<object, string, Task>>());
        lock (handlers)
        {
            handlers.Add((msg, corr) => handler((T)msg, corr));
        }
        _logger.LogInformation("[InMemoryBus] Subscribed handler to topic {Topic} for type {Type}", topic, typeof(T).Name);
        return Task.CompletedTask;
    }

    private async Task ProcessQueueAsync()
    {
        while (await _channel.Reader.WaitToReadAsync())
        {
            while (_channel.Reader.TryRead(out var item))
            {
                if (_subscribers.TryGetValue(item.Topic, out var handlers))
                {
                    List<Func<object, string, Task>> snapshot;
                    lock (handlers)
                    {
                        snapshot = handlers.ToList();
                    }

                    try
                    {
                        var deserialized = JsonSerializer.Deserialize(item.MessageJson, item.TargetType);
                        if (deserialized != null)
                        {
                            foreach (var handler in snapshot)
                            {
                                _ = Task.Run(async () =>
                                {
                                    try
                                    {
                                        await handler(deserialized, item.CorrelationId);
                                    }
                                    catch (Exception ex)
                                    {
                                        _logger.LogError(ex, "Error processing event from topic {Topic}", item.Topic);
                                    }
                                });
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error deserializing message for topic {Topic}", item.Topic);
                    }
                }
            }
        }
    }
}
