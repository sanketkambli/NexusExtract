using System.Buffers.Binary;
using System.Net;
using System.Net.Sockets;
using System.Text;
using Microsoft.Extensions.Logging;

namespace NexusExtract.MockSources;

/// <summary>
/// Lightweight TCP Socket Server emulating a real Geneva Java Socket listener.
/// Listens on 127.0.0.1:9100 and streams length-prefixed binary portfolio frames.
/// </summary>
public sealed class GenevaMockServer : IDisposable
{
    private readonly int _port;
    private readonly ILogger? _logger;
    private TcpListener? _listener;
    private CancellationTokenSource? _cts;
    private Task? _listenerTask;

    public GenevaMockServer(int port = 9100, ILogger? logger = null)
    {
        _port = port;
        _logger = logger;
    }

    public void Start()
    {
        _cts = new CancellationTokenSource();
        _listener = new TcpListener(IPAddress.Loopback, _port);
        _listener.Start();
        _logger?.LogInformation("[GenevaMockServer] Listening on 127.0.0.1:{Port}", _port);
        _listenerTask = Task.Run(() => AcceptClientsAsync(_cts.Token));
    }

    private async Task AcceptClientsAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                var client = await _listener!.AcceptTcpClientAsync(ct);
                _ = Task.Run(() => HandleClientAsync(client, ct), ct);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "[GenevaMockServer] Error accepting client");
            }
        }
    }

    private async Task HandleClientAsync(TcpClient client, CancellationToken ct)
    {
        using (client)
        await using (var stream = client.GetStream())
        {
            try
            {
                // 1. Read 4-byte BigEndian Handshake Length Header
                var header = new byte[4];
                var read = await stream.ReadAsync(header.AsMemory(0, 4), ct);
                if (read < 4) return;

                int payloadLen = BinaryPrimitives.ReadInt32BigEndian(header);
                var handshakePayload = new byte[payloadLen];
                await stream.ReadAsync(handshakePayload.AsMemory(0, payloadLen), ct);
                var requestStr = Encoding.UTF8.GetString(handshakePayload);
                _logger?.LogInformation("[GenevaMockServer] Received client query handshake: {Req}", requestStr);

                // 2. Stream 10 frames of 500 Geneva portfolio position records
                var random = new Random(99);
                string[] tickers = ["AAPL", "MSFT", "NVDA", "TSLA", "META", "AMZN", "BABA", "ASML", "TSM"];

                for (int frame = 1; frame <= 10; frame++)
                {
                    var sb = new StringBuilder();
                    if (frame == 1)
                    {
                        sb.AppendLine("Portfolio,SecurityId,Ticker,Custodian,Shares,MarketValue,Currency,AsOfDate");
                    }

                    for (int r = 0; r < 500; r++)
                    {
                        var ticker = tickers[random.Next(tickers.Length)];
                        var shares = random.Next(100, 25000);
                        var mv = shares * (random.NextDouble() * 300 + 20);
                        sb.AppendLine($"PORT-ALPHA,SEC-{ticker}-01,{ticker},BNY_MELLON,{shares},{mv:F2},USD,{DateTime.UtcNow:yyyy-MM-dd}");
                    }

                    var frameBytes = Encoding.UTF8.GetBytes(sb.ToString());
                    var lengthBuf = new byte[4];
                    BinaryPrimitives.WriteInt32BigEndian(lengthBuf, frameBytes.Length);

                    await stream.WriteAsync(lengthBuf, ct);
                    await stream.WriteAsync(frameBytes, ct);
                    await stream.FlushAsync(ct);

                    await Task.Delay(40, ct); // Realistic socket streaming interval
                }

                // 3. Send End of Stream frame (length = 0)
                var eosBuf = new byte[4];
                BinaryPrimitives.WriteInt32BigEndian(eosBuf, 0);
                await stream.WriteAsync(eosBuf, ct);
                await stream.FlushAsync(ct);
            }
            catch (Exception ex)
            {
                _logger?.LogDebug(ex, "[GenevaMockServer] Client disconnected or finished");
            }
        }
    }

    public void Stop()
    {
        _cts?.Cancel();
        _listener?.Stop();
    }

    public void Dispose()
    {
        Stop();
        _cts?.Dispose();
    }
}
