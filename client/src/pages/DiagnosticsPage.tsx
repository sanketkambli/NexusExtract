import React, { useState } from 'react';
import {
  Box,
  Typography,
  Paper,
  Grid,
  TextField,
  Button,
  Chip,
  Alert,
  Table,
  TableBody,
  TableCell,
  TableContainer,
  TableHead,
  TableRow,
  Divider,
} from '@mui/material';
import {
  Search,
  AlertTriangle,
  CheckCircle2,
  HelpCircle,
  Clock,
  Terminal,
  Activity,
  ArrowRight,
  ShieldAlert,
  RotateCcw,
} from 'lucide-react';
import { useJobDiagnostics, useReplayJob } from '../api/jobsApi';
import { JobStatus } from '../types/api';

interface DiagnosticsPageProps {
  initialJobId?: string | null;
}

export const DiagnosticsPage: React.FC<DiagnosticsPageProps> = ({ initialJobId }) => {
  const [searchJobId, setSearchJobId] = useState(initialJobId || '');
  const [activeJobId, setActiveJobId] = useState<string | null>(initialJobId || null);
  const [logFilter, setLogFilter] = useState<'All' | 'Error' | 'Warning' | 'Info' | 'Trace'>('All');

  const { data: diagnostic, isLoading, error } = useJobDiagnostics(activeJobId);
  const replayMutation = useReplayJob();

  const handleSearch = (e: React.FormEvent) => {
    e.preventDefault();
    if (searchJobId.trim()) {
      setActiveJobId(searchJobId.trim());
    }
  };

  const getRootCauseSeverity = (category: string | null) => {
    switch (category) {
      case 'None':
        return 'success';
      case 'DatabaseConcurrency':
      case 'Authentication':
      case 'PartnerSftpTimeout':
        return 'warning';
      case 'SourceProtocolError':
      case 'StorageSinkError':
      case 'DataIntegrityError':
      case 'InfrastructureOrUnknown':
        return 'error';
      default:
        return 'info';
    }
  };

  const filteredLogs = diagnostic?.logs.filter((l) => {
    if (logFilter === 'All') return true;
    return l.logLevel.toLowerCase() === logFilter.toLowerCase();
  }) || [];

  return (
    <Box>
      {/* Header & Search */}
      <Box sx={{ mb: 3 }}>
        <Typography variant="h5" sx={{ fontWeight: 700, color: '#f8fafc' }}>
          Support & Diagnostics Console
        </Typography>
        <Typography variant="body2" sx={{ color: '#94a3b8' }}>
          Self-service troubleshooting for support teams & third-party API consumers. Trace jobs without developer escalation.
        </Typography>
      </Box>

      {/* Search Bar */}
      <Paper sx={{ p: 2, mb: 3, bgcolor: '#0f172a', borderRadius: 3 }}>
        <form onSubmit={handleSearch}>
          <Box sx={{ display: 'flex', gap: 2 }}>
            <TextField
              fullWidth
              size="small"
              placeholder="Enter Job ID (GUID) or Correlation ID..."
              value={searchJobId}
              onChange={(e) => setSearchJobId(e.target.value)}
              InputProps={{
                startAdornment: <Search size={18} color="#94a3b8" style={{ marginRight: 8 }} />,
              }}
              sx={{
                '& .MuiInputBase-root': {
                  fontFamily: '"JetBrains Mono", monospace',
                  fontSize: '0.85rem',
                },
              }}
            />
            <Button
              type="submit"
              variant="contained"
              sx={{
                background: 'linear-gradient(135deg, #0284c7 0%, #38bdf8 100%)',
                color: '#fff',
                px: 3,
                fontWeight: 600,
              }}
            >
              Analyze Job
            </Button>
          </Box>
        </form>
      </Paper>

      {isLoading && (
        <Typography sx={{ color: '#94a3b8', py: 4, textAlign: 'center' }}>
          Retrieving execution trace and analyzing diagnostic logs...
        </Typography>
      )}

      {error && (
        <Alert severity="error" sx={{ mb: 3, bgcolor: 'rgba(239, 68, 68, 0.1)', color: '#ef4444' }}>
          Failed to fetch diagnostics for Job ID '{activeJobId}': {(error as Error).message}
        </Alert>
      )}

      {diagnostic && (
        <Box>
          {/* Diagnostic Summary Cards */}
          <Grid container spacing={3} sx={{ mb: 3 }}>
            {/* Left: Root Cause Analysis */}
            <Grid item xs={12} md={7}>
              <Paper
                sx={{
                  p: 3,
                  bgcolor: '#0f172a',
                  borderRadius: 3,
                  border: '1px solid rgba(255,255,255,0.06)',
                  height: '100%',
                }}
              >
                <Box sx={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', mb: 2 }}>
                  <Typography variant="h6" sx={{ color: '#f8fafc', fontWeight: 600 }}>
                    Root Cause Diagnosis
                  </Typography>
                  <Chip
                    label={`Category: ${diagnostic.rootCauseCategory || 'Unknown'}`}
                    color={getRootCauseSeverity(diagnostic.rootCauseCategory) as any}
                    variant="outlined"
                    sx={{ fontWeight: 600 }}
                  />
                </Box>

                {/* Remediation Advice Box */}
                <Box
                  sx={{
                    p: 2,
                    borderRadius: 2,
                    bgcolor: 'rgba(56, 189, 248, 0.08)',
                    border: '1px solid rgba(56, 189, 248, 0.2)',
                    mb: 2,
                  }}
                >
                  <Typography variant="subtitle2" sx={{ color: '#38bdf8', fontWeight: 600, mb: 0.5 }}>
                    Actionable Remediation (No Dev Needed):
                  </Typography>
                  <Typography variant="body2" sx={{ color: '#f8fafc' }}>
                    {diagnostic.suggestedRemediation}
                  </Typography>
                </Box>

                {(diagnostic.status === 'Failed' || diagnostic.status === 'DeadLettered') && (
                  <Button
                    variant="contained"
                    startIcon={<RotateCcw size={16} />}
                    onClick={() => replayMutation.mutate(diagnostic.jobId)}
                    disabled={replayMutation.isPending}
                    sx={{
                      background: 'linear-gradient(135deg, #f59e0b 0%, #d97706 100%)',
                      color: '#fff',
                      fontWeight: 600,
                      mb: 2,
                      width: '100%',
                    }}
                  >
                    {replayMutation.isPending ? 'Re-queueing Job...' : 'Replay Job from Dead-Letter Queue'}
                  </Button>
                )}

                {diagnostic.errorMessage && (
                  <Box sx={{ mt: 2 }}>
                    <Typography variant="caption" sx={{ color: '#ef4444', fontWeight: 600, display: 'block', mb: 0.5 }}>
                      Error Message:
                    </Typography>
                    <Box
                      sx={{
                        p: 1.5,
                        borderRadius: 2,
                        bgcolor: 'rgba(239, 68, 68, 0.08)',
                        color: '#fca5a5',
                        fontFamily: '"JetBrains Mono", monospace',
                        fontSize: '0.78rem',
                      }}
                    >
                      {diagnostic.errorMessage}
                    </Box>
                  </Box>
                )}
              </Paper>
            </Grid>

            {/* Right: Job Execution Metrics */}
            <Grid item xs={12} md={5}>
              <Paper
                sx={{
                  p: 3,
                  bgcolor: '#0f172a',
                  borderRadius: 3,
                  border: '1px solid rgba(255,255,255,0.06)',
                  height: '100%',
                }}
              >
                <Typography variant="h6" sx={{ color: '#f8fafc', fontWeight: 600, mb: 2 }}>
                  Execution Context
                </Typography>

                <Box sx={{ display: 'flex', flexDirection: 'column', gap: 1.5 }}>
                  <Box sx={{ display: 'flex', justifyContent: 'space-between' }}>
                    <Typography variant="body2" sx={{ color: '#94a3b8' }}>Job Name:</Typography>
                    <Typography variant="body2" sx={{ color: '#f8fafc', fontWeight: 600 }}>{diagnostic.jobName}</Typography>
                  </Box>
                  <Box sx={{ display: 'flex', justifyContent: 'space-between' }}>
                    <Typography variant="body2" sx={{ color: '#94a3b8' }}>Source System:</Typography>
                    <Chip label={diagnostic.sourceType} size="small" sx={{ bgcolor: 'rgba(255,255,255,0.05)', color: '#cbd5e1' }} />
                  </Box>
                  <Box sx={{ display: 'flex', justifyContent: 'space-between' }}>
                    <Typography variant="body2" sx={{ color: '#94a3b8' }}>Destination Sink:</Typography>
                    <Chip label={diagnostic.sinkType} size="small" sx={{ bgcolor: 'rgba(56, 189, 248, 0.1)', color: '#38bdf8' }} />
                  </Box>
                  <Box sx={{ display: 'flex', justifyContent: 'space-between' }}>
                    <Typography variant="body2" sx={{ color: '#94a3b8' }}>Correlation ID:</Typography>
                    <Typography variant="caption" sx={{ color: '#38bdf8', fontFamily: '"JetBrains Mono", monospace' }}>
                      {diagnostic.correlationId}
                    </Typography>
                  </Box>
                  <Box sx={{ display: 'flex', justifyContent: 'space-between' }}>
                    <Typography variant="body2" sx={{ color: '#94a3b8' }}>Total Streamed:</Typography>
                    <Typography variant="body2" sx={{ color: '#f8fafc', fontWeight: 600 }}>
                      {(diagnostic.bytesTransferred / (1024 * 1024)).toFixed(2)} MB ({diagnostic.rowsTransferred.toLocaleString()} records)
                    </Typography>
                  </Box>
                  <Box sx={{ display: 'flex', justifyContent: 'space-between' }}>
                    <Typography variant="body2" sx={{ color: '#94a3b8' }}>Started At:</Typography>
                    <Typography variant="caption" sx={{ color: '#94a3b8' }}>
                      {diagnostic.startedAtUtc ? new Date(diagnostic.startedAtUtc).toLocaleTimeString() : 'N/A'}
                    </Typography>
                  </Box>
                </Box>
              </Paper>
            </Grid>
          </Grid>

          {/* Execution Log Stream */}
          <Paper sx={{ p: 3, bgcolor: '#0f172a', borderRadius: 3, border: '1px solid rgba(255,255,255,0.06)' }}>
            <Box sx={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', mb: 2 }}>
              <Typography variant="h6" sx={{ color: '#f8fafc', fontWeight: 600, display: 'flex', alignItems: 'center', gap: 1 }}>
                <Terminal size={18} color="#38bdf8" /> Granular Execution Audit Logs
              </Typography>

              {/* Log Level Filters */}
              <Box sx={{ display: 'flex', gap: 1 }}>
                {(['All', 'Error', 'Warning', 'Info', 'Trace'] as const).map((lvl) => (
                  <Chip
                    key={lvl}
                    label={lvl}
                    size="small"
                    onClick={() => setLogFilter(lvl)}
                    sx={{
                      cursor: 'pointer',
                      bgcolor: logFilter === lvl ? '#38bdf8' : 'rgba(255,255,255,0.05)',
                      color: logFilter === lvl ? '#070b14' : '#94a3b8',
                      fontWeight: 600,
                    }}
                  />
                ))}
              </Box>
            </Box>

            <TableContainer sx={{ maxHeight: 400 }}>
              <Table size="small">
                <TableHead sx={{ bgcolor: 'rgba(255,255,255,0.02)' }}>
                  <TableRow>
                    <TableCell sx={{ color: '#94a3b8', fontSize: '0.75rem', width: 140 }}>TIMESTAMP (UTC)</TableCell>
                    <TableCell sx={{ color: '#94a3b8', fontSize: '0.75rem', width: 90 }}>LEVEL</TableCell>
                    <TableCell sx={{ color: '#94a3b8', fontSize: '0.75rem', width: 120 }}>STAGE</TableCell>
                    <TableCell sx={{ color: '#94a3b8', fontSize: '0.75rem' }}>MESSAGE & DETAIL</TableCell>
                  </TableRow>
                </TableHead>
                <TableBody>
                  {filteredLogs.length === 0 ? (
                    <TableRow>
                      <TableCell colSpan={4} sx={{ textAlign: 'center', color: '#64748b', py: 4 }}>
                        No logs matching filter '{logFilter}'.
                      </TableCell>
                    </TableRow>
                  ) : (
                    filteredLogs.map((log) => (
                      <TableRow
                        key={log.id}
                        sx={{
                          '&:hover': { bgcolor: 'rgba(255,255,255,0.02)' },
                          borderBottom: '1px solid rgba(255,255,255,0.03)',
                        }}
                      >
                        <TableCell sx={{ color: '#64748b', fontSize: '0.72rem', fontFamily: '"JetBrains Mono", monospace' }}>
                          {new Date(log.timestampUtc).toLocaleTimeString()}
                        </TableCell>
                        <TableCell>
                          <Chip
                            label={log.logLevel}
                            size="small"
                            sx={{
                              fontSize: '0.68rem',
                              height: 20,
                              fontWeight: 600,
                              bgcolor:
                                log.logLevel === 'Error'
                                  ? 'rgba(239, 68, 68, 0.15)'
                                  : log.logLevel === 'Warning'
                                  ? 'rgba(245, 158, 11, 0.15)'
                                  : log.logLevel === 'Trace'
                                  ? 'rgba(168, 85, 247, 0.15)'
                                  : 'rgba(56, 189, 248, 0.15)',
                              color:
                                log.logLevel === 'Error'
                                  ? '#ef4444'
                                  : log.logLevel === 'Warning'
                                  ? '#f59e0b'
                                  : log.logLevel === 'Trace'
                                  ? '#c084fc'
                                  : '#38bdf8',
                            }}
                          />
                        </TableCell>
                        <TableCell>
                          <Typography variant="caption" sx={{ color: '#cbd5e1', fontWeight: 500 }}>
                            {log.stage}
                          </Typography>
                        </TableCell>
                        <TableCell>
                          <Typography variant="body2" sx={{ color: '#f8fafc', fontSize: '0.8rem' }}>
                            {log.message}
                          </Typography>
                          {log.exceptionDetails && (
                            <Typography
                              variant="caption"
                              sx={{
                                color: '#f87171',
                                display: 'block',
                                mt: 0.5,
                                fontFamily: '"JetBrains Mono", monospace',
                                fontSize: '0.7rem',
                                whiteSpace: 'pre-wrap',
                              }}
                            >
                              {log.exceptionDetails}
                            </Typography>
                          )}
                        </TableCell>
                      </TableRow>
                    ))
                  )}
                </TableBody>
              </Table>
            </TableContainer>
          </Paper>
        </Box>
      )}
    </Box>
  );
};
