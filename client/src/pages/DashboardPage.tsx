import React from 'react';
import {
  Box,
  Typography,
  Paper,
  Table,
  TableBody,
  TableCell,
  TableContainer,
  TableHead,
  TableRow,
  Chip,
  Button,
  LinearProgress,
  IconButton,
  Tooltip,
} from '@mui/material';
import {
  Play,
  RotateCw,
  AlertCircle,
  AlertOctagon,
  CheckCircle2,
  Clock,
  ArrowUpRight,
  ExternalLink,
  Ban,
  RefreshCw,
} from 'lucide-react';
import { JobStatusResponse, JobStatus } from '../types/api';
import { useCancelJob, useReplayJob } from '../api/jobsApi';

interface DashboardPageProps {
  jobs: JobStatusResponse[];
  isLoading: boolean;
  onSelectJobForDiagnostics: (jobId: string) => void;
  onOpenTriggerModal: () => void;
}

export const DashboardPage: React.FC<DashboardPageProps> = ({
  jobs,
  isLoading,
  onSelectJobForDiagnostics,
  onOpenTriggerModal,
}) => {
  const cancelMutation = useCancelJob();
  const replayMutation = useReplayJob();

  const getStatusBadge = (status: JobStatus, retryCount: number, maxRetries: number) => {
    switch (status) {
      case 'Completed':
        return (
          <Chip
            icon={<CheckCircle2 size={14} color="#10b981" />}
            label="Completed"
            size="small"
            sx={{
              bgcolor: 'rgba(16, 185, 129, 0.12)',
              color: '#10b981',
              fontWeight: 600,
              fontSize: '0.75rem',
              border: '1px solid rgba(16, 185, 129, 0.3)',
            }}
          />
        );
      case 'StreamingToSink':
        return (
          <Chip
            icon={<RotateCw size={14} color="#38bdf8" className="spin-animation" />}
            label="Streaming to Sink"
            size="small"
            sx={{
              bgcolor: 'rgba(56, 189, 248, 0.12)',
              color: '#38bdf8',
              fontWeight: 600,
              fontSize: '0.75rem',
              border: '1px solid rgba(56, 189, 248, 0.3)',
            }}
          />
        );
      case 'Extracting':
        return (
          <Chip
            icon={<RotateCw size={14} color="#a855f7" className="spin-animation" />}
            label="Extracting Cursor"
            size="small"
            sx={{
              bgcolor: 'rgba(168, 85, 247, 0.12)',
              color: '#a855f7',
              fontWeight: 600,
              fontSize: '0.75rem',
              border: '1px solid rgba(168, 85, 247, 0.3)',
            }}
          />
        );
      case 'Connecting':
        return (
          <Chip
            icon={<Clock size={14} color="#06b6d4" />}
            label="Connecting"
            size="small"
            sx={{
              bgcolor: 'rgba(6, 182, 212, 0.12)',
              color: '#06b6d4',
              fontWeight: 600,
              fontSize: '0.75rem',
            }}
          />
        );
      case 'Queued':
      case 'Pending':
        return (
          <Chip
            label={retryCount > 0 ? `Queued (Retry ${retryCount}/${maxRetries})` : 'Queued (Kafka/PubSub)'}
            size="small"
            sx={{
              bgcolor: 'rgba(148, 163, 184, 0.12)',
              color: '#94a3b8',
              fontWeight: 600,
              fontSize: '0.75rem',
            }}
          />
        );
      case 'Failed':
        return (
          <Chip
            icon={<AlertCircle size={14} color="#ef4444" />}
            label={retryCount > 0 ? `Failed (${retryCount}/${maxRetries})` : 'Failed'}
            size="small"
            sx={{
              bgcolor: 'rgba(239, 68, 68, 0.12)',
              color: '#ef4444',
              fontWeight: 600,
              fontSize: '0.75rem',
              border: '1px solid rgba(239, 68, 68, 0.3)',
            }}
          />
        );
      case 'DeadLettered':
        return (
          <Chip
            icon={<AlertOctagon size={14} color="#f43f5e" />}
            label="Dead-Lettered (DLQ)"
            size="small"
            sx={{
              bgcolor: 'rgba(244, 63, 94, 0.15)',
              color: '#f43f5e',
              fontWeight: 700,
              fontSize: '0.75rem',
              border: '1px solid rgba(244, 63, 94, 0.4)',
            }}
          />
        );
      case 'Cancelled':
        return (
          <Chip
            label="Cancelled"
            size="small"
            sx={{
              bgcolor: 'rgba(100, 116, 139, 0.12)',
              color: '#64748b',
              fontWeight: 600,
              fontSize: '0.75rem',
            }}
          />
        );
    }
  };

  const formatBytes = (bytes: number) => {
    if (bytes === 0) return '0 B';
    const k = 1024;
    const sizes = ['B', 'KB', 'MB', 'GB', 'TB'];
    const i = Math.floor(Math.log(bytes) / Math.log(k));
    return parseFloat((bytes / Math.pow(k, i)).toFixed(2)) + ' ' + sizes[i];
  };

  return (
    <Box>
      {/* Action Bar */}
      <Box sx={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', mb: 2 }}>
        <Box>
          <Typography variant="h6" sx={{ fontWeight: 600, color: '#f8fafc' }}>
            Live Extractions & Streaming Pipeline
          </Typography>
          <Typography variant="caption" sx={{ color: '#64748b' }}>
            Real-time multi-source data ingestion monitoring with auto-refresh
          </Typography>
        </Box>
        <Button
          variant="contained"
          startIcon={<Play size={16} />}
          onClick={onOpenTriggerModal}
          sx={{
            background: 'linear-gradient(135deg, #0284c7 0%, #38bdf8 100%)',
            color: '#fff',
            px: 2.5,
          }}
        >
          New Extraction Job
        </Button>
      </Box>

      {/* Table */}
      <TableContainer
        component={Paper}
        sx={{
          bgcolor: '#0f172a',
          borderRadius: 3,
          border: '1px solid rgba(255,255,255,0.06)',
          overflow: 'hidden',
        }}
      >
        <Table sx={{ minWidth: 850 }}>
          <TableHead sx={{ bgcolor: 'rgba(255,255,255,0.02)' }}>
            <TableRow>
              <TableCell sx={{ color: '#94a3b8', fontWeight: 600, fontSize: '0.8rem' }}>JOB & CORRELATION</TableCell>
              <TableCell sx={{ color: '#94a3b8', fontWeight: 600, fontSize: '0.8rem' }}>SOURCE SYSTEM</TableCell>
              <TableCell sx={{ color: '#94a3b8', fontWeight: 600, fontSize: '0.8rem' }}>TARGET SINK</TableCell>
              <TableCell sx={{ color: '#94a3b8', fontWeight: 600, fontSize: '0.8rem' }}>STATUS</TableCell>
              <TableCell sx={{ color: '#94a3b8', fontWeight: 600, fontSize: '0.8rem' }}>STREAMED PAYLOAD</TableCell>
              <TableCell sx={{ color: '#94a3b8', fontWeight: 600, fontSize: '0.8rem' }}>OUTPUT & CHECKSUM</TableCell>
              <TableCell sx={{ color: '#94a3b8', fontWeight: 600, fontSize: '0.8rem', textAlign: 'right' }}>ACTIONS</TableCell>
            </TableRow>
          </TableHead>
          <TableBody>
            {jobs.length === 0 ? (
              <TableRow>
                <TableCell colSpan={7} sx={{ textAlign: 'center', py: 6, color: '#64748b' }}>
                  No extractions recorded yet. Click "New Extraction Job" to trigger live data extraction!
                </TableCell>
              </TableRow>
            ) : (
              jobs.map((job) => {
                const isRunning = job.status === 'Connecting' || job.status === 'Extracting' || job.status === 'StreamingToSink';
                return (
                  <TableRow
                    key={job.jobId}
                    sx={{
                      '&:hover': { bgcolor: 'rgba(255,255,255,0.02)' },
                      borderBottom: '1px solid rgba(255,255,255,0.04)',
                    }}
                  >
                    {/* Job Name & Correlation ID */}
                    <TableCell>
                      <Typography variant="body2" sx={{ fontWeight: 600, color: '#f8fafc' }}>
                        {job.jobName}
                      </Typography>
                      <Typography
                        variant="caption"
                        sx={{
                          fontFamily: '"JetBrains Mono", monospace',
                          color: '#64748b',
                          fontSize: '0.72rem',
                        }}
                      >
                        CID: {job.correlationId}
                      </Typography>
                    </TableCell>

                    {/* Source System */}
                    <TableCell>
                      <Chip
                        label={job.sourceType}
                        size="small"
                        sx={{
                          bgcolor: 'rgba(255,255,255,0.05)',
                          color: '#cbd5e1',
                          fontWeight: 500,
                          fontSize: '0.75rem',
                        }}
                      />
                    </TableCell>

                    {/* Target Sink */}
                    <TableCell>
                      <Chip
                        label={job.sinkType}
                        size="small"
                        sx={{
                          bgcolor: 'rgba(56, 189, 248, 0.08)',
                          color: '#38bdf8',
                          fontWeight: 500,
                          fontSize: '0.75rem',
                        }}
                      />
                    </TableCell>

                    {/* Status */}
                    <TableCell>
                      {getStatusBadge(job.status, job.retryCount, job.maxRetries)}
                      {isRunning && (
                        <LinearProgress
                          sx={{
                            mt: 1,
                            borderRadius: 1,
                            height: 3,
                            bgcolor: 'rgba(56, 189, 248, 0.1)',
                            '& .MuiLinearProgress-bar': { bgcolor: '#38bdf8' },
                          }}
                        />
                      )}
                    </TableCell>

                    {/* Volume & Records */}
                    <TableCell>
                      <Typography variant="body2" sx={{ fontWeight: 600, color: '#f8fafc' }}>
                        {formatBytes(job.bytesExtracted)}
                      </Typography>
                      <Typography variant="caption" sx={{ color: '#64748b', fontSize: '0.72rem' }}>
                        {job.rowsExtracted.toLocaleString()} records
                      </Typography>
                    </TableCell>

                    {/* Output Path & Checksum */}
                    <TableCell sx={{ maxWidth: 220 }}>
                      {job.targetOutputPath ? (
                        <Box>
                          <Typography
                            variant="caption"
                            sx={{
                              color: '#94a3b8',
                              display: 'block',
                              whiteSpace: 'nowrap',
                              overflow: 'hidden',
                              textOverflow: 'ellipsis',
                              fontFamily: '"JetBrains Mono", monospace',
                              fontSize: '0.7rem',
                            }}
                          >
                            {job.targetOutputPath}
                          </Typography>
                          {job.checksumSha256 && (
                            <Typography
                              variant="caption"
                              sx={{
                                color: '#10b981',
                                fontFamily: '"JetBrains Mono", monospace',
                                fontSize: '0.68rem',
                              }}
                            >
                              SHA256: {job.checksumSha256.substring(0, 10)}...
                            </Typography>
                          )}
                        </Box>
                      ) : (
                        <Typography variant="caption" sx={{ color: '#475569' }}>
                          Pending stream sync...
                        </Typography>
                      )}
                    </TableCell>

                    {/* Actions */}
                    <TableCell sx={{ textAlign: 'right' }}>
                      <Box sx={{ display: 'flex', justifyContent: 'flex-end', gap: 1 }}>
                        {(job.status === 'Failed' || job.status === 'DeadLettered') && (
                          <Tooltip title="Replay from DLQ / Re-queue Extraction">
                            <Button
                              size="small"
                              variant="outlined"
                              startIcon={<RefreshCw size={13} />}
                              onClick={() => replayMutation.mutate(job.jobId)}
                              disabled={replayMutation.isPending}
                              sx={{
                                color: '#f59e0b',
                                borderColor: 'rgba(245, 158, 11, 0.4)',
                                fontSize: '0.73rem',
                                px: 1.2,
                                py: 0.4,
                                '&:hover': {
                                  borderColor: '#f59e0b',
                                  bgcolor: 'rgba(245, 158, 11, 0.1)',
                                },
                              }}
                            >
                              Replay
                            </Button>
                          </Tooltip>
                        )}

                        <Button
                          size="small"
                          variant="outlined"
                          onClick={() => onSelectJobForDiagnostics(job.jobId)}
                          sx={{
                            color: '#38bdf8',
                            borderColor: 'rgba(56, 189, 248, 0.3)',
                            fontSize: '0.75rem',
                            px: 1.5,
                            py: 0.5,
                            '&:hover': {
                              borderColor: '#38bdf8',
                              bgcolor: 'rgba(56, 189, 248, 0.1)',
                            },
                          }}
                        >
                          Diagnose
                        </Button>

                        {isRunning && (
                          <Tooltip title="Cancel Extraction Job">
                            <IconButton
                              size="small"
                              onClick={() => cancelMutation.mutate(job.jobId)}
                              sx={{ color: '#ef4444' }}
                            >
                              <Ban size={16} />
                            </IconButton>
                          </Tooltip>
                        )}
                      </Box>
                    </TableCell>
                  </TableRow>
                );
              })
            )}
          </TableBody>
        </Table>
      </TableContainer>
    </Box>
  );
};
