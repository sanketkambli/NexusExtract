using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using NexusExtract.Domain.Common;
using NexusExtract.Domain.Entities;
using NexusExtract.Domain.Enums;
using NexusExtract.Domain.Interfaces;

namespace NexusExtract.Infrastructure.Sinks;

public sealed class LocalDiskSinkConfig
{
    public string BaseDirectory { get; set; } = "./extracted_data/local_sync";
    public string FileNamePattern { get; set; } = "{JobId}_{JobName}_{Timestamp}.dat";
    public bool GzipCompress { get; set; } = false;
}

public sealed class LocalDiskSink : IFileSink
{
    private readonly ILogger<LocalDiskSink> _logger;

    public SinkType SupportedType => SinkType.LocalDisk;

    public LocalDiskSink(ILogger<LocalDiskSink> logger)
    {
        _logger = logger;
    }

    public async Task<SinkWriteResult> WriteStreamAsync(
        ExtractionJob job,
        IAsyncEnumerable<DataChunk> stream,
        Action<long, long> onProgress,
        CancellationToken ct = default)
    {
        var config = ParseConfig(job.SinkConfigJson);
        var dir = Path.GetFullPath(config.BaseDirectory);
        Directory.CreateDirectory(dir);

        var finalFileName = $"{job.JobName.Replace(' ', '_')}_{job.Id:N}_{DateTime.UtcNow:yyyyMMddHHmmss}.csv";
        var tempFilePath = Path.Combine(dir, $"{finalFileName}.tmp");
        var finalFilePath = Path.Combine(dir, finalFileName);

        _logger.LogInformation("Writing local file sink stream to {Path}", tempFilePath);

        long totalBytesWritten = 0;
        long totalRecords = 0;
        using var sha256 = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);

        await using (var fileStream = new FileStream(
            tempFilePath,
            FileMode.Create,
            FileAccess.Write,
            FileShare.None,
            bufferSize: 128 * 1024,
            useAsync: true))
        {
            await foreach (var chunk in stream.WithCancellation(ct))
            {
                if (chunk.Payload.Length > 0)
                {
                    await fileStream.WriteAsync(chunk.Payload, ct);
                    sha256.AppendData(chunk.Payload.Span);
                    totalBytesWritten += chunk.Payload.Length;
                }

                totalRecords += chunk.RecordCount;
                onProgress(totalBytesWritten, totalRecords);

                if (chunk.IsFinal)
                {
                    break;
                }
            }

            await fileStream.FlushAsync(ct);
        }

        // Atomically rename .tmp to final file
        if (File.Exists(finalFilePath))
        {
            File.Delete(finalFilePath);
        }
        File.Move(tempFilePath, finalFilePath);

        var hashBytes = sha256.GetHashAndReset();
        var checksumHex = Convert.ToHexString(hashBytes).ToLowerInvariant();

        _logger.LogInformation("Local file sink finalized: {Path} ({Bytes:N0} bytes, {Records:N0} records, SHA256: {Checksum})",
            finalFilePath, totalBytesWritten, totalRecords, checksumHex);

        return new SinkWriteResult(
            true,
            finalFilePath,
            totalBytesWritten,
            totalRecords,
            checksumHex
        );
    }

    private static LocalDiskSinkConfig ParseConfig(string json)
    {
        try
        {
            return JsonSerializer.Deserialize<LocalDiskSinkConfig>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
                ?? new LocalDiskSinkConfig();
        }
        catch
        {
            return new LocalDiskSinkConfig();
        }
    }
}
