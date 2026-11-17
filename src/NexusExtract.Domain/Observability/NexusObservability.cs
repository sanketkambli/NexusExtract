using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace NexusExtract.Domain.Observability;

public static class NexusObservability
{
    public const string ServiceName = "NexusExtractGateway";
    public const string ServiceVersion = "1.0.0";

    // Distributed Tracing ActivitySource
    public static readonly ActivitySource ActivitySource = new(ServiceName, ServiceVersion);

    // Application Metrics Meter
    public static readonly Meter Meter = new(ServiceName, ServiceVersion);

    // Metrics Counters & Histograms
    public static readonly Counter<long> JobsStartedCounter = Meter.CreateCounter<long>(
        "extractions.jobs.started",
        description: "Total number of extraction jobs started");

    public static readonly Counter<long> JobsCompletedCounter = Meter.CreateCounter<long>(
        "extractions.jobs.completed",
        description: "Total number of extraction jobs completed successfully");

    public static readonly Counter<long> JobsFailedCounter = Meter.CreateCounter<long>(
        "extractions.jobs.failed",
        description: "Total number of extraction jobs that failed");

    public static readonly UpDownCounter<long> ActiveJobsCounter = Meter.CreateUpDownCounter<long>(
        "extractions.jobs.active",
        description: "Current number of in-flight active extraction jobs");

    public static readonly Counter<long> BytesStreamedCounter = Meter.CreateCounter<long>(
        "extractions.bytes.streamed",
        unit: "By",
        description: "Total bytes streamed from sources to sinks");

    public static readonly Counter<long> RecordsStreamedCounter = Meter.CreateCounter<long>(
        "extractions.records.streamed",
        unit: "{records}",
        description: "Total records streamed from sources to sinks");

    public static readonly Histogram<double> JobDurationHistogram = Meter.CreateHistogram<double>(
        "extractions.job.duration.seconds",
        unit: "s",
        description: "Duration of extraction pipeline execution in seconds");

    public static readonly Histogram<double> AdapterLatencyHistogram = Meter.CreateHistogram<double>(
        "extractions.adapter.latency.ms",
        unit: "ms",
        description: "Source connection and query latency in milliseconds");

    public static readonly Counter<long> DeadLetterMessagesCounter = Meter.CreateCounter<long>(
        "extractions.dlq.messages.total",
        description: "Total messages routed to the Dead-Letter Queue (DLQ)");
}
