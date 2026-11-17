namespace NexusExtract.Domain.Common;

/// <summary>
/// Represents an in-flight streaming chunk of extracted payload.
/// Designed for zero-copy streaming pipeline without loading entire 100+ GB files into memory.
/// </summary>
public sealed class DataChunk
{
    public long SequenceNumber { get; init; }
    public ReadOnlyMemory<byte> Payload { get; init; }
    public long RecordCount { get; init; }
    public bool IsFinal { get; init; }
    public string? CompressionCodec { get; init; }

    public static DataChunk FromBytes(long sequenceNumber, byte[] bytes, long recordCount = 0, bool isFinal = false)
    {
        return new DataChunk
        {
            SequenceNumber = sequenceNumber,
            Payload = bytes,
            RecordCount = recordCount,
            IsFinal = isFinal
        };
    }
}
