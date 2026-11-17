export type SourceType = 'GenevaSocket' | 'GoTradeSybase' | 'PartnerSftpDrop' | 'GenericRestApi';
export type SinkType = 'LocalDisk' | 'CloudStorageS3Minio' | 'NetworkShare';
export type JobStatus = 'Pending' | 'Queued' | 'Connecting' | 'Extracting' | 'StreamingToSink' | 'Completed' | 'Failed' | 'Cancelled' | 'DeadLettered';

export interface JobStatusResponse {
  jobId: string;
  jobName: string;
  correlationId: string;
  sourceType: SourceType;
  sinkType: SinkType;
  status: JobStatus;
  bytesExtracted: number;
  rowsExtracted: number;
  targetOutputPath: string | null;
  checksumSha256: string | null;
  errorMessage: string | null;
  retryCount: number;
  maxRetries: number;
  createdAtUtc: string;
  startedAtUtc: string | null;
  completedAtUtc: string | null;
  initiatedBy: string;
}

export interface JobExecutionLog {
  id: number;
  jobId: string;
  correlationId: string;
  timestampUtc: string;
  logLevel: 'Info' | 'Warning' | 'Error' | 'Trace';
  stage: string;
  message: string;
  exceptionDetails: string | null;
  bytesTransferredSoFar: number;
}

export interface JobDiagnosticResponse {
  jobId: string;
  jobName: string;
  correlationId: string;
  sourceType: SourceType;
  sinkType: SinkType;
  status: JobStatus;
  errorMessage: string | null;
  rootCauseCategory: string | null;
  suggestedRemediation: string | null;
  createdAtUtc: string;
  completedAtUtc: string | null;
  startedAtUtc: string | null;
  bytesTransferred: number;
  rowsTransferred: number;
  logs: JobExecutionLog[];
}

export interface DataSourceDto {
  id: string;
  name: string;
  sourceType: SourceType;
  description: string;
  connectionConfigJson: string;
  isActive: boolean;
  lastHealthCheckUtc: string | null;
  healthStatus: 'Healthy' | 'Degraded' | 'Unreachable' | 'Unknown';
  lastHealthMessage: string | null;
  lastResponseTime: string | null;
}

export interface TriggerExtractionRequest {
  jobName: string;
  sourceType: SourceType;
  sinkType: SinkType;
  sourceConfigJson: string;
  sinkConfigJson: string;
  correlationId?: string;
  initiatedBy?: string;
}

export interface ConnectionHealthResult {
  isSuccess: boolean;
  message: string;
  latency: string;
  metadata?: Record<string, string>;
}

// Phase 3: Gemini AI & RAG Schema Mapping
export interface CanonicalFieldDefinition {
  fieldName: string;
  dataType: string;
  isRequired: boolean;
  description: string;
  exampleValue: string;
  knownSynonyms: string[];
}

export interface FieldMappingRule {
  sourceColumn: string;
  targetCanonicalField: string;
  confidenceScore: number;
  transformationRule: string;
  aiRationale: string;
  isUserOverridden?: boolean;
}

export interface AnalyzeSchemaRequest {
  partnerName: string;
  sourceColumns: string[];
  sampleRows?: Record<string, string>[];
  correlationId?: string;
}

export interface AnalyzeSchemaResponse {
  sessionId: string;
  partnerName: string;
  correlationId: string;
  suggestedMappings: FieldMappingRule[];
  modelUsed: string;
  promptTokens: number;
  completionTokens: number;
  estimatedCostUsd: number;
  processingTimeMs: number;
  ragContextSummary: string;
}

export interface ApproveSchemaRequest {
  finalMappings: FieldMappingRule[];
  approvedBy: string;
}

export interface SchemaMappingSession {
  id: string;
  partnerName: string;
  correlationId: string;
  mappings: FieldMappingRule[];
  status: 'PendingReview' | 'Approved' | 'Rejected';
  aiModelUsed: string;
  promptTokens: number;
  completionTokens: number;
  estimatedCostUsd: number;
  processingTimeMs: number;
  createdAtUtc: string;
  approvedAtUtc?: string | null;
  approvedBy?: string | null;
}
