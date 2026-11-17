using System.Security.Cryptography;
using System.Text.Json;
using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Logging;
using NexusExtract.Domain.Common;
using NexusExtract.Domain.Entities;
using NexusExtract.Domain.Enums;
using NexusExtract.Domain.Interfaces;

namespace NexusExtract.Infrastructure.Sinks;

public sealed class S3MinioCloudConfig
{
    public string ServiceUrl { get; set; } = "http://127.0.0.1:9000";
    public string BucketName { get; set; } = "nexus-extractions";
    public string KeyPrefix { get; set; } = "daily_extracts";
    public string AccessKey { get; set; } = "minioadmin";
    public string SecretKey { get; set; } = "minioadmin";
    public bool ForcePathStyle { get; set; } = true;
    public bool FallbackToSimulatedCloudIfUnavailable { get; set; } = true;
    public string SimulatedCloudPath { get; set; } = "./extracted_data/cloud_storage_simulation";
}

public sealed class S3MinioCloudSink : IFileSink
{
    private readonly ILogger<S3MinioCloudSink> _logger;

    public SinkType SupportedType => SinkType.CloudStorageS3Minio;

    public S3MinioCloudSink(ILogger<S3MinioCloudSink> logger)
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
        var objectKey = $"{config.KeyPrefix.TrimEnd('/')}/{job.JobName}_{job.Id:N}.csv";

        _logger.LogInformation("Initiating Cloud Storage (S3/MinIO/GCP) stream for {Bucket}/{Key}", config.BucketName, objectKey);

        bool useSimulation = config.FallbackToSimulatedCloudIfUnavailable;
        AmazonS3Client? s3Client = null;

        try
        {
            var s3Config = new AmazonS3Config
            {
                ServiceURL = config.ServiceUrl,
                ForcePathStyle = config.ForcePathStyle,
                UseHttp = config.ServiceUrl.StartsWith("http://", StringComparison.OrdinalIgnoreCase),
                Timeout = TimeSpan.FromSeconds(30)
            };

            s3Client = new AmazonS3Client(config.AccessKey, config.SecretKey, s3Config);
            // Quick check
            await s3Client.ListBucketsAsync(ct);
            useSimulation = false;
        }
        catch (Exception ex)
        {
            _logger.LogWarning("Remote Cloud Storage endpoint unavailable ({Msg}). Using Cloud Simulation: {Sim}", ex.Message, useSimulation);
            if (!useSimulation)
            {
                throw;
            }
        }

        if (!useSimulation && s3Client != null)
        {
            using (s3Client)
            {
                // Real S3 Multipart Upload
                var initRequest = new InitiateMultipartUploadRequest
                {
                    BucketName = config.BucketName,
                    Key = objectKey
                };

                var initResponse = await s3Client.InitiateMultipartUploadAsync(initRequest, ct);
                var uploadId = initResponse.UploadId;
                var partETags = new List<PartETag>();

                long totalBytes = 0;
                long totalRecords = 0;
                int partNumber = 1;
                using var sha256 = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);

                var partBuffer = new MemoryStream(10 * 1024 * 1024); // 10MB minimum part size

                try
                {
                    await foreach (var chunk in stream.WithCancellation(ct))
                    {
                        if (chunk.Payload.Length > 0)
                        {
                            await partBuffer.WriteAsync(chunk.Payload, ct);
                            sha256.AppendData(chunk.Payload.Span);
                            totalBytes += chunk.Payload.Length;
                        }

                        totalRecords += chunk.RecordCount;
                        onProgress(totalBytes, totalRecords);

                        if (partBuffer.Length >= 5 * 1024 * 1024) // 5MB part threshold for S3
                        {
                            partBuffer.Position = 0;
                            var uploadPartRequest = new UploadPartRequest
                            {
                                BucketName = config.BucketName,
                                Key = objectKey,
                                UploadId = uploadId,
                                PartNumber = partNumber,
                                InputStream = partBuffer,
                                PartSize = partBuffer.Length
                            };

                            var uploadPartResponse = await s3Client.UploadPartAsync(uploadPartRequest, ct);
                            partETags.Add(new PartETag(partNumber++, uploadPartResponse.ETag));
                            partBuffer.SetLength(0);
                        }

                        if (chunk.IsFinal) break;
                    }

                    if (partBuffer.Length > 0)
                    {
                        partBuffer.Position = 0;
                        var uploadPartRequest = new UploadPartRequest
                        {
                            BucketName = config.BucketName,
                            Key = objectKey,
                            UploadId = uploadId,
                            PartNumber = partNumber,
                            InputStream = partBuffer,
                            PartSize = partBuffer.Length
                        };

                        var uploadPartResponse = await s3Client.UploadPartAsync(uploadPartRequest, ct);
                        partETags.Add(new PartETag(partNumber, uploadPartResponse.ETag));
                    }

                    var completeRequest = new CompleteMultipartUploadRequest
                    {
                        BucketName = config.BucketName,
                        Key = objectKey,
                        UploadId = uploadId,
                        PartETags = partETags
                    };

                    await s3Client.CompleteMultipartUploadAsync(completeRequest, ct);
                    var checksumHex = Convert.ToHexString(sha256.GetHashAndReset()).ToLowerInvariant();

                    return new SinkWriteResult(
                        true,
                        $"s3://{config.BucketName}/{objectKey}",
                        totalBytes,
                        totalRecords,
                        checksumHex
                    );
                }
                catch
                {
                    await s3Client.AbortMultipartUploadAsync(new AbortMultipartUploadRequest
                    {
                        BucketName = config.BucketName,
                        Key = objectKey,
                        UploadId = uploadId
                    }, CancellationToken.None);
                    throw;
                }
            }
        }
        else
        {
            // Simulated cloud storage (e.g. s3://bucket/key written to local storage mirroring S3 structure)
            var cloudDir = Path.Combine(config.SimulatedCloudPath, config.BucketName, config.KeyPrefix);
            Directory.CreateDirectory(cloudDir);
            var filePath = Path.Combine(cloudDir, $"{job.JobName}_{job.Id:N}.csv");

            long totalBytes = 0;
            long totalRecords = 0;
            using var sha256 = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);

            await using (var fileStream = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None, 128 * 1024, true))
            {
                await foreach (var chunk in stream.WithCancellation(ct))
                {
                    if (chunk.Payload.Length > 0)
                    {
                        await fileStream.WriteAsync(chunk.Payload, ct);
                        sha256.AppendData(chunk.Payload.Span);
                        totalBytes += chunk.Payload.Length;
                    }

                    totalRecords += chunk.RecordCount;
                    onProgress(totalBytes, totalRecords);

                    if (chunk.IsFinal) break;
                }
                await fileStream.FlushAsync(ct);
            }

            var checksumHex = Convert.ToHexString(sha256.GetHashAndReset()).ToLowerInvariant();
            return new SinkWriteResult(
                true,
                $"gs://{config.BucketName}/{objectKey} (Simulated at {Path.GetFullPath(filePath)})",
                totalBytes,
                totalRecords,
                checksumHex
            );
        }
    }

    private static S3MinioCloudConfig ParseConfig(string json)
    {
        try
        {
            return JsonSerializer.Deserialize<S3MinioCloudConfig>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
                ?? new S3MinioCloudConfig();
        }
        catch
        {
            return new S3MinioCloudConfig();
        }
    }
}
