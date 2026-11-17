using System.Buffers.Binary;
using System.Diagnostics;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using NexusExtract.Domain.Common;
using NexusExtract.Domain.Entities;
using NexusExtract.Domain.Enums;
using NexusExtract.Domain.Interfaces;

namespace NexusExtract.Adapters.Geneva;

public sealed class GenevaSocketConfig
{
    public string Host { get; set; } = "127.0.0.1";
    public int Port { get; set; } = 9100;
    public string AuthToken { get; set; } = "GENEVA_DEV_TOKEN";
    public string PortfolioId { get; set; } = "GLOBAL_EQUITIES";
    public int ConnectTimeoutMs { get; set; } = 5000;
    public int ReadTimeoutMs { get; set; } = 15000;
    public int ChunkSizeRecords { get; set; } = 1000;
}

public sealed class GenevaSocketAdapter : IDataSourceAdapter
{
    private readonly ILogger<GenevaSocketAdapter> _logger;

    public SourceType SupportedType => SourceType.GenevaSocket;

    public GenevaSocketAdapter(ILogger<GenevaSocketAdapter> logger)
    {
        _logger = logger;
    }

    public async Task<ConnectionHealthResult> TestConnectionAsync(string connectionConfigJson, CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
        var config = ParseConfig(connectionConfigJson);

        try
        {
            using var client = new TcpClient();
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(config.ConnectTimeoutMs);

            await client.ConnectAsync(config.Host, config.Port, cts.Token);
            sw.Stop();

            return new ConnectionHealthResult(
                true,
                $"Successfully connected to Geneva Java Socket listener at {config.Host}:{config.Port}",
                sw.Elapsed,
                new Dictionary<string, string> { ["Host"] = config.Host, ["Port"] = config.Port.ToString() }
            );
        }
        catch (Exception ex)
        {
            sw.Stop();
            _logger.LogWarning(ex, "Geneva socket health probe failed for {Host}:{Port}", config.Host, config.Port);
            return new ConnectionHealthResult(
                false,
                $"Failed to connect to Geneva socket at {config.Host}:{config.Port}: {ex.Message}",
                sw.Elapsed
            );
        }
    }

    public async IAsyncEnumerable<DataChunk> ExtractStreamAsync(
        ExtractionJob job,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        var config = ParseConfig(job.SourceConfigJson);
        _logger.LogInformation("Opening Geneva TCP socket stream to {Host}:{Port} for job {JobId}", config.Host, config.Port, job.Id);

        using var client = new TcpClient();
        await client.ConnectAsync(config.Host, config.Port, ct);
        using var networkStream = client.GetStream();

        // 1. Send Handshake & Query Command: [4 bytes length][JSON payload]
        var handshakePayload = JsonSerializer.Serialize(new
        {
            action = "EXTRACT_STREAM",
            portfolioId = config.PortfolioId,
            token = config.AuthToken,
            jobId = job.Id.ToString(),
            correlationId = job.CorrelationId
        });

        var handshakeBytes = Encoding.UTF8.GetBytes(handshakePayload);
        var lengthHeader = new byte[4];
        BinaryPrimitives.WriteInt32BigEndian(lengthHeader, handshakeBytes.Length);

        await networkStream.WriteAsync(lengthHeader, ct);
        await networkStream.WriteAsync(handshakeBytes, ct);
        await networkStream.FlushAsync(ct);

        // 2. Stream Response Frames:
        // Framing Protocol: [4-byte big-endian payload length] + [Payload]
        // Length == 0 indicates End-Of-Stream
        long sequence = 0;
        var headerBuffer = new byte[4];

        while (!ct.IsCancellationRequested)
        {
            var bytesRead = await ReadExactBytesAsync(networkStream, headerBuffer, 4, ct);
            if (bytesRead < 4)
            {
                break; // Socket closed gracefully
            }

            var frameLength = BinaryPrimitives.ReadInt32BigEndian(headerBuffer);
            if (frameLength <= 0)
            {
                // End of Stream signal from Geneva
                yield return DataChunk.FromBytes(++sequence, Array.Empty<byte>(), 0, isFinal: true);
                yield break;
            }

            var frameBuffer = new byte[frameLength];
            await ReadExactBytesAsync(networkStream, frameBuffer, frameLength, ct);

            // Frame contains delimited position records
            long recordsInFrame = CountLineBreaks(frameBuffer);
            yield return DataChunk.FromBytes(++sequence, frameBuffer, recordsInFrame, isFinal: false);
        }
    }

    private static async Task<int> ReadExactBytesAsync(NetworkStream stream, byte[] buffer, int count, CancellationToken ct)
    {
        int totalRead = 0;
        while (totalRead < count)
        {
            var read = await stream.ReadAsync(buffer.AsMemory(totalRead, count - totalRead), ct);
            if (read == 0) return totalRead;
            totalRead += read;
        }
        return totalRead;
    }

    private static long CountLineBreaks(byte[] bytes)
    {
        long count = 0;
        for (int i = 0; i < bytes.Length; i++)
        {
            if (bytes[i] == (byte)'\n') count++;
        }
        return count == 0 ? 1 : count;
    }

    private static GenevaSocketConfig ParseConfig(string json)
    {
        try
        {
            return JsonSerializer.Deserialize<GenevaSocketConfig>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
                ?? new GenevaSocketConfig();
        }
        catch
        {
            return new GenevaSocketConfig();
        }
    }
}
