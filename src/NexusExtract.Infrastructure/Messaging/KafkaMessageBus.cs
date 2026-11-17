using System.Text;
using System.Text.Json;
using Confluent.Kafka;
using Microsoft.Extensions.Logging;
using NexusExtract.Domain.Interfaces;

namespace NexusExtract.Infrastructure.Messaging;

public sealed class KafkaConfig
{
    public string BootstrapServers { get; set; } = "127.0.0.1:9092";
    public string GroupId { get; set; } = "nexus-extraction-workers";
    public bool AutoOffsetResetEarliest { get; set; } = true;
}

public sealed class KafkaMessageBus : IMessageBus, IDisposable
{
    private readonly IProducer<string, string> _producer;
    private readonly KafkaConfig _config;
    private readonly ILogger<KafkaMessageBus> _logger;

    public KafkaMessageBus(KafkaConfig config, ILogger<KafkaMessageBus> logger)
    {
        _config = config;
        _logger = logger;

        var producerConfig = new ProducerConfig
        {
            BootstrapServers = config.BootstrapServers,
            Acks = Acks.All,
            EnableIdempotence = true,
            MessageTimeoutMs = 5000
        };

        _producer = new ProducerBuilder<string, string>(producerConfig).Build();
    }

    public async Task PublishAsync<T>(string topic, T message, string correlationId, CancellationToken ct = default) where T : class
    {
        var json = JsonSerializer.Serialize(message);
        var headers = new Headers
        {
            { "X-Correlation-ID", Encoding.UTF8.GetBytes(correlationId) },
            { "X-Message-Type", Encoding.UTF8.GetBytes(typeof(T).FullName ?? typeof(T).Name) }
        };

        var kafkaMessage = new Message<string, string>
        {
            Key = correlationId,
            Value = json,
            Headers = headers
        };

        _logger.LogInformation("[Kafka] Publishing message to {Topic} with Key={Key}", topic, correlationId);
        await _producer.ProduceAsync(topic, kafkaMessage, ct);
    }

    public Task SubscribeAsync<T>(string topic, Func<T, string, Task> handler, CancellationToken ct = default) where T : class
    {
        // Background consumer loop
        Task.Run(async () =>
        {
            var consumerConfig = new ConsumerConfig
            {
                BootstrapServers = _config.BootstrapServers,
                GroupId = _config.GroupId,
                AutoOffsetReset = _config.AutoOffsetResetEarliest ? AutoOffsetReset.Earliest : AutoOffsetReset.Latest,
                EnableAutoCommit = false
            };

            using var consumer = new ConsumerBuilder<string, string>(consumerConfig).Build();
            consumer.Subscribe(topic);

            try
            {
                while (!ct.IsCancellationRequested)
                {
                    try
                    {
                        var consumeResult = consumer.Consume(ct);
                        if (consumeResult?.Message != null)
                        {
                            string correlationId = "UNKNOWN";
                            if (consumeResult.Message.Headers.TryGetLastBytes("X-Correlation-ID", out var corrBytes))
                            {
                                correlationId = Encoding.UTF8.GetString(corrBytes);
                            }

                            var deserialized = JsonSerializer.Deserialize<T>(consumeResult.Message.Value);
                            if (deserialized != null)
                            {
                                await handler(deserialized, correlationId);
                            }

                            consumer.Commit(consumeResult);
                        }
                    }
                    catch (ConsumeException ex)
                    {
                        _logger.LogWarning(ex, "[Kafka] Consume error on topic {Topic}", topic);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // Normal shutdown
            }
            finally
            {
                consumer.Close();
            }
        }, ct);

        return Task.CompletedTask;
    }

    public void Dispose()
    {
        _producer?.Flush(TimeSpan.FromSeconds(5));
        _producer?.Dispose();
    }
}
