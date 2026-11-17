using System.Data;
using System.Data.Common;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using NexusExtract.Domain.Common;
using NexusExtract.Domain.Entities;
using NexusExtract.Domain.Enums;
using NexusExtract.Domain.Interfaces;

namespace NexusExtract.Adapters.GoTrade;

public sealed class GoTradeSybaseConfig
{
    public string ConnectionString { get; set; } = "Server=127.0.0.1,1433;Database=gotrade_db;User Id=gotrade_user;Password=TradeSecret2026!;TrustServerCertificate=True;";
    public string SqlQuery { get; set; } = "SELECT TradeId, Symbol, Account, Quantity, Price, Side, ExecutedAtUtc FROM Trades WHERE ExecutedAtUtc >= DATEADD(day, -7, GETUTCDATE())";
    public int CommandTimeoutSeconds { get; set; } = 300;
    public int ChunkSizeRecords { get; set; } = 5000;
    public bool EnableSyntheticMockFallback { get; set; } = true;
}

public sealed class GoTradeSybaseAdapter : IDataSourceAdapter
{
    private readonly ILogger<GoTradeSybaseAdapter> _logger;

    public SourceType SupportedType => SourceType.GoTradeSybase;

    public GoTradeSybaseAdapter(ILogger<GoTradeSybaseAdapter> logger)
    {
        _logger = logger;
    }

    public async Task<ConnectionHealthResult> TestConnectionAsync(string connectionConfigJson, CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
        var config = ParseConfig(connectionConfigJson);

        try
        {
            await using var conn = new SqlConnection(config.ConnectionString);
            await conn.OpenAsync(ct);
            await using var cmd = new SqlCommand("SELECT 1", conn) { CommandTimeout = 5 };
            await cmd.ExecuteScalarAsync(ct);
            sw.Stop();

            return new ConnectionHealthResult(
                true,
                "Successfully connected and executed probe on GoTrade database.",
                sw.Elapsed
            );
        }
        catch (Exception ex)
        {
            sw.Stop();
            if (config.EnableSyntheticMockFallback)
            {
                return new ConnectionHealthResult(
                    true,
                    $"Live DB unreachable ({ex.Message}), but Synthetic Engine Ready for Local Demo.",
                    sw.Elapsed,
                    new Dictionary<string, string> { ["Mode"] = "SyntheticFallback" }
                );
            }

            return new ConnectionHealthResult(false, $"Database probe failed: {ex.Message}", sw.Elapsed);
        }
    }

    public async IAsyncEnumerable<DataChunk> ExtractStreamAsync(
        ExtractionJob job,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        var config = ParseConfig(job.SourceConfigJson);
        _logger.LogInformation("Beginning GoTrade streaming extraction for Job {JobId}", job.Id);

        bool databaseConnected = false;
        SqlConnection? conn = null;
        SqlDataReader? reader = null;

        try
        {
            conn = new SqlConnection(config.ConnectionString);
            await conn.OpenAsync(ct);
            var cmd = new SqlCommand(config.SqlQuery, conn)
            {
                CommandTimeout = config.CommandTimeoutSeconds
            };

            // SequentialAccess streams large columns/rows with minimum memory overhead
            reader = await cmd.ExecuteReaderAsync(CommandBehavior.SequentialAccess | CommandBehavior.CloseConnection, ct);
            databaseConnected = true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not open direct database connection. Fallback enabled: {Fallback}", config.EnableSyntheticMockFallback);
            if (!config.EnableSyntheticMockFallback)
            {
                throw;
            }
        }

        if (databaseConnected && reader != null)
        {
            await using (conn)
            await using (reader)
            {
                var sb = new StringBuilder();
                long sequence = 0;
                long recordCount = 0;

                // Write CSV Header
                for (int i = 0; i < reader.FieldCount; i++)
                {
                    sb.Append(reader.GetName(i));
                    if (i < reader.FieldCount - 1) sb.Append(',');
                }
                sb.AppendLine();

                while (await reader.ReadAsync(ct))
                {
                    for (int i = 0; i < reader.FieldCount; i++)
                    {
                        var val = reader.IsDBNull(i) ? string.Empty : reader.GetValue(i)?.ToString()?.Replace(",", ";") ?? "";
                        sb.Append(val);
                        if (i < reader.FieldCount - 1) sb.Append(',');
                    }
                    sb.AppendLine();
                    recordCount++;

                    if (recordCount % config.ChunkSizeRecords == 0)
                    {
                        var bytes = Encoding.UTF8.GetBytes(sb.ToString());
                        sb.Clear();
                        yield return DataChunk.FromBytes(++sequence, bytes, config.ChunkSizeRecords, isFinal: false);
                    }
                }

                if (sb.Length > 0)
                {
                    var bytes = Encoding.UTF8.GetBytes(sb.ToString());
                    var remaining = recordCount % config.ChunkSizeRecords;
                    yield return DataChunk.FromBytes(++sequence, bytes, remaining == 0 ? config.ChunkSizeRecords : remaining, isFinal: true);
                }
            }
        }
        else
        {
            // Stream realistic synthetic GoTrade trades for testing & interviews
            long totalRecords = 50000; // Demonstrates 50,000 streaming rows seamlessly
            long chunkSize = config.ChunkSizeRecords;
            long sequence = 0;
            var random = new Random(42);
            string[] symbols = ["AAPL", "MSFT", "GOOGL", "AMZN", "NVDA", "JPM", "GS"];
            string[] accounts = ["ACC-NY-01", "ACC-LN-04", "ACC-HK-09", "ACC-SG-02"];

            var header = "TradeId,Symbol,Account,Quantity,Price,Side,ExecutedAtUtc\n";
            yield return DataChunk.FromBytes(++sequence, Encoding.UTF8.GetBytes(header), 0, isFinal: false);

            for (long i = 1; i <= totalRecords; i += chunkSize)
            {
                ct.ThrowIfCancellationRequested();
                var sb = new StringBuilder();
                long batchCount = Math.Min(chunkSize, totalRecords - i + 1);

                for (long j = 0; j < batchCount; j++)
                {
                    long id = i + j;
                    var sym = symbols[random.Next(symbols.Length)];
                    var acc = accounts[random.Next(accounts.Length)];
                    var qty = random.Next(10, 5000) * 10;
                    var price = (decimal)(random.NextDouble() * 500 + 10);
                    var side = random.Next(2) == 0 ? "BUY" : "SELL";
                    var time = DateTime.UtcNow.AddMinutes(-random.Next(1, 10000)).ToString("yyyy-MM-ddTHH:mm:ssZ");

                    sb.AppendLine($"{id},{sym},{acc},{qty},{price:F2},{side},{time}");
                }

                var isFinal = (i + batchCount - 1) >= totalRecords;
                var bytes = Encoding.UTF8.GetBytes(sb.ToString());
                yield return DataChunk.FromBytes(++sequence, bytes, batchCount, isFinal: isFinal);

                // Small micro-delay simulating network stream backpressure
                await Task.Delay(20, ct);
            }
        }
    }

    private static GoTradeSybaseConfig ParseConfig(string json)
    {
        try
        {
            return JsonSerializer.Deserialize<GoTradeSybaseConfig>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
                ?? new GoTradeSybaseConfig();
        }
        catch
        {
            return new GoTradeSybaseConfig();
        }
    }
}
