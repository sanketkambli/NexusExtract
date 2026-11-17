using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using NexusExtract.Domain.Common;
using NexusExtract.Domain.Entities;
using NexusExtract.Domain.Enums;
using NexusExtract.Domain.Interfaces;
using Renci.SshNet;

namespace NexusExtract.Adapters.SftpDrop;

public sealed class PartnerSftpConfig
{
    public string Host { get; set; } = "127.0.0.1";
    public int Port { get; set; } = 2222;
    public string Username { get; set; } = "partner_client";
    public string Password { get; set; } = "PartnerSecure2026!";
    public string InboundRemotePath { get; set; } = "/inbound";
    public string OutboundRemotePath { get; set; } = "/outbound";
    public int PollingIntervalSeconds { get; set; } = 2;
    public int MaxPollingWaitSeconds { get; set; } = 30;
    public bool UseLocalFileSystemEmulation { get; set; } = true;
    public string LocalEmulationPath { get; set; } = "./storage_emulator/sftp";
}

public sealed class PartnerSftpAdapter : IDataSourceAdapter
{
    private readonly ILogger<PartnerSftpAdapter> _logger;

    public SourceType SupportedType => SourceType.PartnerSftpDrop;

    public PartnerSftpAdapter(ILogger<PartnerSftpAdapter> logger)
    {
        _logger = logger;
    }

    public async Task<ConnectionHealthResult> TestConnectionAsync(string connectionConfigJson, CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
        var config = ParseConfig(connectionConfigJson);

        if (config.UseLocalFileSystemEmulation)
        {
            Directory.CreateDirectory(Path.Combine(config.LocalEmulationPath, "inbound"));
            Directory.CreateDirectory(Path.Combine(config.LocalEmulationPath, "outbound"));
            sw.Stop();
            return new ConnectionHealthResult(
                true,
                $"Local SFTP Emulation Active at '{Path.GetFullPath(config.LocalEmulationPath)}'",
                sw.Elapsed,
                new Dictionary<string, string> { ["Mode"] = "FileSystemEmulation" }
            );
        }

        try
        {
            using var client = new SftpClient(config.Host, config.Port, config.Username, config.Password);
            await client.ConnectAsync(ct);
            var isConnected = client.IsConnected;
            client.Disconnect();
            sw.Stop();

            return new ConnectionHealthResult(
                isConnected,
                isConnected ? "Connected to remote partner SFTP successfully." : "Could not connect to partner SFTP.",
                sw.Elapsed
            );
        }
        catch (Exception ex)
        {
            sw.Stop();
            return new ConnectionHealthResult(false, $"Partner SFTP connection failed: {ex.Message}", sw.Elapsed);
        }
    }

    public async IAsyncEnumerable<DataChunk> ExtractStreamAsync(
        ExtractionJob job,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        var config = ParseConfig(job.SourceConfigJson);
        var requestFileName = $"REQ_{job.Id:N}.ctl";
        var responseFileName = $"RESP_{job.Id:N}.csv";

        _logger.LogInformation("Initiating Partner SFTP File-Drop workflow for Job {JobId}. Request file: {Req}", job.Id, requestFileName);

        if (config.UseLocalFileSystemEmulation)
        {
            var inboundDir = Path.Combine(config.LocalEmulationPath, "inbound");
            var outboundDir = Path.Combine(config.LocalEmulationPath, "outbound");
            Directory.CreateDirectory(inboundDir);
            Directory.CreateDirectory(outboundDir);

            // 1. Drop request file
            var requestFilePath = Path.Combine(inboundDir, requestFileName);
            var requestContent = JsonSerializer.Serialize(new
            {
                jobId = job.Id,
                correlationId = job.CorrelationId,
                action = "EXTRACT_SETTLEMENT_FILE",
                timestamp = DateTime.UtcNow
            }, new JsonSerializerOptions { WriteIndented = true });

            await File.WriteAllTextAsync(requestFilePath, requestContent, ct);
            _logger.LogInformation("Dropped partner request file at {Path}", requestFilePath);

            // 2. Partner processing simulation: automatically generate response file in outbound if not present
            var responseFilePath = Path.Combine(outboundDir, responseFileName);
            await GenerateSyntheticPartnerResponseFileAsync(responseFilePath, job, ct);

            // 3. Stream response file in chunks
            await using var fileStream = new FileStream(responseFilePath, FileMode.Open, FileAccess.Read, FileShare.Read, 64 * 1024, useAsync: true);
            var buffer = new byte[64 * 1024]; // 64 KB chunks
            long sequence = 0;
            int bytesRead;

            while ((bytesRead = await fileStream.ReadAsync(buffer.AsMemory(0, buffer.Length), ct)) > 0)
            {
                var chunkBytes = new byte[bytesRead];
                Buffer.BlockCopy(buffer, 0, chunkBytes, 0, bytesRead);
                var isFinal = fileStream.Position >= fileStream.Length;
                yield return DataChunk.FromBytes(++sequence, chunkBytes, 50, isFinal);
            }
        }
        else
        {
            // Real Remote SFTP Server implementation using SSH.NET
            using var client = new SftpClient(config.Host, config.Port, config.Username, config.Password);
            await client.ConnectAsync(ct);

            // 1. Upload control file
            var reqContent = Encoding.UTF8.GetBytes($"JOB_ID={job.Id}\nCORRELATION={job.CorrelationId}\nTIMESTAMP={DateTime.UtcNow:O}\n");
            using (var ms = new MemoryStream(reqContent))
            {
                var remoteInboundPath = $"{config.InboundRemotePath}/{requestFileName}";
                client.UploadFile(ms, remoteInboundPath);
            }

            // 2. Poll for response file
            var remoteOutboundPath = $"{config.OutboundRemotePath}/{responseFileName}";
            var sw = Stopwatch.StartNew();
            bool found = false;

            while (sw.Elapsed < TimeSpan.FromSeconds(config.MaxPollingWaitSeconds))
            {
                ct.ThrowIfCancellationRequested();
                if (client.Exists(remoteOutboundPath))
                {
                    found = true;
                    break;
                }
                await Task.Delay(TimeSpan.FromSeconds(config.PollingIntervalSeconds), ct);
            }

            if (!found)
            {
                throw new TimeoutException($"Partner SFTP response file '{responseFileName}' was not delivered within {config.MaxPollingWaitSeconds}s timeout.");
            }

            // 3. Stream file from remote SFTP server
            using var remoteStream = client.OpenRead(remoteOutboundPath);
            var buffer = new byte[64 * 1024];
            long sequence = 0;
            int bytesRead;

            while ((bytesRead = await remoteStream.ReadAsync(buffer.AsMemory(0, buffer.Length), ct)) > 0)
            {
                var chunkBytes = new byte[bytesRead];
                Buffer.BlockCopy(buffer, 0, chunkBytes, 0, bytesRead);
                var isFinal = remoteStream.Position >= remoteStream.Length;
                yield return DataChunk.FromBytes(++sequence, chunkBytes, 50, isFinal);
            }

            client.Disconnect();
        }
    }

    private static async Task GenerateSyntheticPartnerResponseFileAsync(string path, ExtractionJob job, CancellationToken ct)
    {
        var sb = new StringBuilder();
        sb.AppendLine("PartnerReference,SettlementId,Currency,Amount,Status,SettledDate");
        for (int i = 1; i <= 2500; i++)
        {
            sb.AppendLine($"PRT-2026-{i:D5},STL-{job.Id:N}-{i:D4},USD,{i * 125.50:F2},SETTLED,{DateTime.UtcNow:yyyy-MM-dd}");
        }
        await File.WriteAllTextAsync(path, sb.ToString(), ct);
    }

    private static PartnerSftpConfig ParseConfig(string json)
    {
        try
        {
            return JsonSerializer.Deserialize<PartnerSftpConfig>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
                ?? new PartnerSftpConfig();
        }
        catch
        {
            return new PartnerSftpConfig();
        }
    }
}
