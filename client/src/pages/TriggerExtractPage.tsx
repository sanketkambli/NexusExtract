import React, { useState } from 'react';
import {
  Box,
  Typography,
  Paper,
  Grid,
  TextField,
  FormControl,
  InputLabel,
  Select,
  MenuItem,
  Button,
  Card,
  CardContent,
  Chip,
  Alert,
} from '@mui/material';
import {
  Play,
  Database,
  Cloud,
  FileText,
  HardDrive,
  Cpu,
  Layers,
} from 'lucide-react';
import { SourceType, SinkType, TriggerExtractionRequest } from '../types/api';
import { useTriggerExtraction } from '../api/jobsApi';

interface TriggerExtractPageProps {
  onSuccessRedirect: () => void;
}

export const TriggerExtractPage: React.FC<TriggerExtractPageProps> = ({ onSuccessRedirect }) => {
  const triggerMutation = useTriggerExtraction();

  const [jobName, setJobName] = useState('Institutional Positions Extraction');
  const [sourceType, setSourceType] = useState<SourceType>('GenevaSocket');
  const [sinkType, setSinkType] = useState<SinkType>('LocalDisk');
  const [correlationId, setCorrelationId] = useState(
    'REQ-' + Math.random().toString(36).substring(2, 9).toUpperCase()
  );

  // Preset Configurations
  const sourcePresets: Record<SourceType, string> = {
    GenevaSocket: JSON.stringify(
      {
        host: '127.0.0.1',
        port: 9100,
        authToken: 'GENEVA_TOKEN_2026',
        portfolioId: 'GLOBAL_EQUITY_PORTFOLIO',
        connectTimeoutMs: 5000,
        readTimeoutMs: 15000,
        chunkSizeRecords: 1000,
      },
      null,
      2
    ),
    GoTradeSybase: JSON.stringify(
      {
        connectionString: 'Server=127.0.0.1,1433;Database=gotrade_db;User Id=sa;Password=Secret123!;TrustServerCertificate=True;',
        sqlQuery: 'SELECT TradeId, Symbol, Account, Quantity, Price, Side, ExecutedAtUtc FROM Trades',
        commandTimeoutSeconds: 300,
        chunkSizeRecords: 5000,
        enableSyntheticMockFallback: true,
      },
      null,
      2
    ),
    PartnerSftpDrop: JSON.stringify(
      {
        host: '127.0.0.1',
        port: 2222,
        username: 'partner_settlement_client',
        password: 'PartnerSecure2026!',
        inboundRemotePath: '/inbound',
        outboundRemotePath: '/outbound',
        pollingIntervalSeconds: 2,
        maxPollingWaitSeconds: 30,
        useLocalFileSystemEmulation: true,
        localEmulationPath: './storage_emulator/sftp',
      },
      null,
      2
    ),
    GenericRestApi: JSON.stringify(
      {
        baseUrl: 'https://api.partnerportal.com/v1',
        endpointPath: '/products/catalog',
        authType: 'ApiKey',
        apiKeyHeader: 'X-API-KEY',
        apiKeyValue: 'prod_api_key_nexus_2026',
        pageSize: 500,
        maxPages: 10,
        enableSyntheticMockFallback: true,
      },
      null,
      2
    ),
  };

  const sinkPresets: Record<SinkType, string> = {
    LocalDisk: JSON.stringify(
      {
        baseDirectory: './extracted_data/local_sync',
        fileNamePattern: '{JobName}_{JobId}_{Timestamp}.csv',
        gzipCompress: false,
      },
      null,
      2
    ),
    CloudStorageS3Minio: JSON.stringify(
      {
        serviceUrl: 'http://127.0.0.1:9000',
        bucketName: 'nexus-enterprise-extracts',
        keyPrefix: 'financial_data/daily',
        accessKey: 'minioadmin',
        secretKey: 'minioadmin',
        fallbackToSimulatedCloudIfUnavailable: true,
        simulatedCloudPath: './extracted_data/cloud_storage_simulation',
      },
      null,
      2
    ),
    NetworkShare: JSON.stringify(
      {
        baseDirectory: './extracted_data/network_share_smb',
        fileNamePattern: '{JobName}_{JobId}_{Timestamp}.csv',
      },
      null,
      2
    ),
  };

  const [sourceConfigJson, setSourceConfigJson] = useState(sourcePresets['GenevaSocket']);
  const [sinkConfigJson, setSinkConfigJson] = useState(sinkPresets['LocalDisk']);

  const handleSourceChange = (newSource: SourceType) => {
    setSourceType(newSource);
    setSourceConfigJson(sourcePresets[newSource]);
    if (newSource === 'GenevaSocket') setJobName('Geneva Portfolio Position Extract');
    if (newSource === 'GoTradeSybase') setJobName('GoTrade Sybase Trade History Extraction');
    if (newSource === 'PartnerSftpDrop') setJobName('Partner SFTP Settlement Extract');
    if (newSource === 'GenericRestApi') setJobName('Global Product Catalogue REST Extract');
  };

  const handleSinkChange = (newSink: SinkType) => {
    setSinkType(newSink);
    setSinkConfigJson(sinkPresets[newSink]);
  };

  const handleSubmit = (e: React.FormEvent) => {
    e.preventDefault();
    const req: TriggerExtractionRequest = {
      jobName,
      sourceType,
      sinkType,
      sourceConfigJson,
      sinkConfigJson,
      correlationId,
      initiatedBy: 'BusinessOperator',
    };

    triggerMutation.mutate(req, {
      onSuccess: () => {
        onSuccessRedirect();
      },
    });
  };

  return (
    <Box>
      <Box sx={{ mb: 3 }}>
        <Typography variant="h5" sx={{ fontWeight: 700, color: '#f8fafc' }}>
          Trigger Data Extraction Job
        </Typography>
        <Typography variant="body2" sx={{ color: '#94a3b8' }}>
          Initiate scalable, asynchronous streaming extraction across legacy socket, database, partner file-drop, or REST sources
        </Typography>
      </Box>

      {triggerMutation.isError && (
        <Alert severity="error" sx={{ mb: 3, bgcolor: 'rgba(239, 68, 68, 0.1)', color: '#ef4444' }}>
          Failed to dispatch extraction job: {(triggerMutation.error as Error).message}
        </Alert>
      )}

      <form onSubmit={handleSubmit}>
        <Grid container spacing={3}>
          {/* Left Column: Source Selection */}
          <Grid item xs={12} md={6}>
            <Paper sx={{ p: 3, bgcolor: '#0f172a', borderRadius: 3 }}>
              <Typography variant="h6" sx={{ color: '#f8fafc', mb: 2, display: 'flex', alignItems: 'center', gap: 1 }}>
                <Database size={20} color="#38bdf8" /> 1. Select Source System
              </Typography>

              <Grid container spacing={1.5} sx={{ mb: 3 }}>
                {[
                  { id: 'GenevaSocket', name: 'Geneva Java Sockets', sub: 'TCP Frame Streaming' },
                  { id: 'GoTradeSybase', name: 'GoTrade Sybase ASE', sub: 'Relational Cursor Streaming' },
                  { id: 'PartnerSftpDrop', name: 'Partner SFTP Drop', sub: 'Request/Response Handshake' },
                  { id: 'GenericRestApi', name: 'Generic REST API', sub: 'OAuth2 / API Key Paging' },
                ].map((s) => (
                  <Grid item xs={6} key={s.id}>
                    <Card
                      onClick={() => handleSourceChange(s.id as SourceType)}
                      sx={{
                        cursor: 'pointer',
                        bgcolor: sourceType === s.id ? 'rgba(56, 189, 248, 0.12)' : 'rgba(255,255,255,0.02)',
                        borderColor: sourceType === s.id ? '#38bdf8' : 'rgba(255,255,255,0.06)',
                        borderWidth: 1,
                        borderStyle: 'solid',
                        '&:hover': { borderColor: '#38bdf8' },
                      }}
                    >
                      <CardContent sx={{ p: 1.5, '&:last-child': { pb: 1.5 } }}>
                        <Typography variant="subtitle2" sx={{ fontWeight: 600, color: '#f8fafc' }}>
                          {s.name}
                        </Typography>
                        <Typography variant="caption" sx={{ color: '#94a3b8' }}>
                          {s.sub}
                        </Typography>
                      </CardContent>
                    </Card>
                  </Grid>
                ))}
              </Grid>

              <Typography variant="subtitle2" sx={{ color: '#94a3b8', mb: 1 }}>
                Source Parameters (JSON Configuration)
              </Typography>
              <TextField
                fullWidth
                multiline
                rows={10}
                value={sourceConfigJson}
                onChange={(e) => setSourceConfigJson(e.target.value)}
                sx={{
                  '& .MuiInputBase-root': {
                    fontFamily: '"JetBrains Mono", monospace',
                    fontSize: '0.8rem',
                    bgcolor: '#070b14',
                  },
                }}
              />
            </Paper>
          </Grid>

          {/* Right Column: Destination Sink & Metadata */}
          <Grid item xs={12} md={6}>
            <Paper sx={{ p: 3, bgcolor: '#0f172a', borderRadius: 3 }}>
              <Typography variant="h6" sx={{ color: '#f8fafc', mb: 2, display: 'flex', alignItems: 'center', gap: 1 }}>
                <Cloud size={20} color="#10b981" /> 2. Destination Sink (FileSync)
              </Typography>

              <Grid container spacing={1.5} sx={{ mb: 3 }}>
                {[
                  { id: 'LocalDisk', name: 'Local / SMB Network Disk', sub: 'High-speed zero-copy IO' },
                  { id: 'CloudStorageS3Minio', name: 'Cloud Storage (GCP / S3)', sub: 'Multipart resumable upload' },
                ].map((sink) => (
                  <Grid item xs={6} key={sink.id}>
                    <Card
                      onClick={() => handleSinkChange(sink.id as SinkType)}
                      sx={{
                        cursor: 'pointer',
                        bgcolor: sinkType === sink.id ? 'rgba(16, 185, 129, 0.12)' : 'rgba(255,255,255,0.02)',
                        borderColor: sinkType === sink.id ? '#10b981' : 'rgba(255,255,255,0.06)',
                        borderWidth: 1,
                        borderStyle: 'solid',
                        '&:hover': { borderColor: '#10b981' },
                      }}
                    >
                      <CardContent sx={{ p: 1.5, '&:last-child': { pb: 1.5 } }}>
                        <Typography variant="subtitle2" sx={{ fontWeight: 600, color: '#f8fafc' }}>
                          {sink.name}
                        </Typography>
                        <Typography variant="caption" sx={{ color: '#94a3b8' }}>
                          {sink.sub}
                        </Typography>
                      </CardContent>
                    </Card>
                  </Grid>
                ))}
              </Grid>

              <Typography variant="subtitle2" sx={{ color: '#94a3b8', mb: 1 }}>
                Sink Parameters (JSON Configuration)
              </Typography>
              <TextField
                fullWidth
                multiline
                rows={4}
                value={sinkConfigJson}
                onChange={(e) => setSinkConfigJson(e.target.value)}
                sx={{
                  mb: 3,
                  '& .MuiInputBase-root': {
                    fontFamily: '"JetBrains Mono", monospace',
                    fontSize: '0.8rem',
                    bgcolor: '#070b14',
                  },
                }}
              />

              <Typography variant="h6" sx={{ color: '#f8fafc', mb: 2, display: 'flex', alignItems: 'center', gap: 1 }}>
                <FileText size={20} color="#a855f7" /> 3. Job Execution Metadata
              </Typography>

              <TextField
                fullWidth
                label="Extraction Job Name"
                value={jobName}
                onChange={(e) => setJobName(e.target.value)}
                sx={{ mb: 2 }}
              />

              <TextField
                fullWidth
                label="Correlation ID (Traceability)"
                value={correlationId}
                onChange={(e) => setCorrelationId(e.target.value)}
                helperText="Propagated across Web API, Kafka, background worker, source socket, and destination sink"
                sx={{ mb: 3 }}
              />

              <Button
                type="submit"
                variant="contained"
                size="large"
                fullWidth
                disabled={triggerMutation.isPending}
                startIcon={<Play size={18} />}
                sx={{
                  py: 1.5,
                  background: 'linear-gradient(135deg, #0284c7 0%, #38bdf8 100%)',
                  fontWeight: 700,
                  fontSize: '1rem',
                  color: '#fff',
                }}
              >
                {triggerMutation.isPending ? 'Queuing Extraction...' : 'Dispatch Extraction Pipeline'}
              </Button>
            </Paper>
          </Grid>
        </Grid>
      </form>
    </Box>
  );
};
