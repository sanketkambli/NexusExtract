import React from 'react';
import {
  Box,
  Typography,
  Paper,
  Grid,
  Chip,
  Button,
  Card,
  CardContent,
  CardActions,
  CircularProgress,
  Tooltip,
} from '@mui/material';
import {
  Database,
  RefreshCw,
  CheckCircle2,
  AlertTriangle,
  Clock,
  Radio,
  Server,
  Key,
} from 'lucide-react';
import { useSources, useTestConnection } from '../api/jobsApi';

export const SourcesPage: React.FC = () => {
  const { data: sources, isLoading } = useSources();
  const testMutation = useTestConnection();

  const handleTest = (sourceId: string) => {
    testMutation.mutate(sourceId);
  };

  return (
    <Box>
      <Box sx={{ mb: 3 }}>
        <Typography variant="h5" sx={{ fontWeight: 700, color: '#f8fafc' }}>
          Extensible Source Adapters
        </Typography>
        <Typography variant="body2" sx={{ color: '#94a3b8' }}>
          Plug-and-play data source adapters implementing the open-closed pipeline interface. Run live connection & latency probes.
        </Typography>
      </Box>

      {isLoading ? (
        <Box sx={{ display: 'flex', justifyContent: 'center', py: 6 }}>
          <CircularProgress size={32} sx={{ color: '#38bdf8' }} />
        </Box>
      ) : (
        <Grid container spacing={3}>
          {sources?.map((s) => {
            const isTesting = testMutation.isPending && testMutation.variables === s.id;
            return (
              <Grid item xs={12} md={6} key={s.id}>
                <Card
                  sx={{
                    bgcolor: '#0f172a',
                    borderRadius: 3,
                    border: '1px solid rgba(255,255,255,0.06)',
                    height: '100%',
                    display: 'flex',
                    flexDirection: 'column',
                    justifyContent: 'space-between',
                  }}
                >
                  <CardContent sx={{ p: 3 }}>
                    {/* Header */}
                    <Box sx={{ display: 'flex', justifyContent: 'space-between', alignItems: 'flex-start', mb: 2 }}>
                      <Box sx={{ display: 'flex', alignItems: 'center', gap: 1.5 }}>
                        <Box
                          sx={{
                            p: 1.2,
                            borderRadius: 2,
                            bgcolor: 'rgba(56, 189, 248, 0.1)',
                            display: 'flex',
                            alignItems: 'center',
                            justifyContent: 'center',
                          }}
                        >
                          <Server size={20} color="#38bdf8" />
                        </Box>
                        <Box>
                          <Typography variant="subtitle1" sx={{ fontWeight: 700, color: '#f8fafc' }}>
                            {s.name}
                          </Typography>
                          <Typography variant="caption" sx={{ color: '#64748b' }}>
                            Type: {s.sourceType}
                          </Typography>
                        </Box>
                      </Box>

                      <Chip
                        icon={s.healthStatus === 'Healthy' ? <CheckCircle2 size={13} color="#10b981" /> : <AlertTriangle size={13} color="#f59e0b" />}
                        label={s.healthStatus}
                        size="small"
                        sx={{
                          bgcolor: s.healthStatus === 'Healthy' ? 'rgba(16, 185, 129, 0.1)' : 'rgba(245, 158, 11, 0.1)',
                          color: s.healthStatus === 'Healthy' ? '#10b981' : '#f59e0b',
                          fontWeight: 600,
                          fontSize: '0.72rem',
                          border: s.healthStatus === 'Healthy' ? '1px solid rgba(16, 185, 129, 0.3)' : '1px solid rgba(245, 158, 11, 0.3)',
                        }}
                      />
                    </Box>

                    {/* Description */}
                    <Typography variant="body2" sx={{ color: '#94a3b8', mb: 2 }}>
                      {s.description}
                    </Typography>

                    {/* Connection Config Inspector */}
                    <Paper
                      sx={{
                        p: 1.5,
                        bgcolor: '#070b14',
                        borderRadius: 2,
                        border: '1px solid rgba(255,255,255,0.04)',
                        mb: 2,
                      }}
                    >
                      <Typography
                        variant="caption"
                        sx={{
                          color: '#64748b',
                          fontFamily: '"JetBrains Mono", monospace',
                          fontSize: '0.72rem',
                          display: 'block',
                          whiteSpace: 'pre-wrap',
                          maxHeight: 100,
                          overflowY: 'auto',
                        }}
                      >
                        {JSON.stringify(JSON.parse(s.connectionConfigJson || '{}'), null, 2)}
                      </Typography>
                    </Paper>

                    {/* Probe Result & Latency */}
                    {s.lastHealthMessage && (
                      <Box sx={{ mt: 1 }}>
                        <Typography variant="caption" sx={{ color: '#cbd5e1', display: 'flex', alignItems: 'center', gap: 1 }}>
                          <Clock size={13} color="#38bdf8" /> Last Probe: {s.lastHealthMessage}
                        </Typography>
                      </Box>
                    )}
                  </CardContent>

                  <CardActions sx={{ p: 3, pt: 0, justifyContent: 'flex-end' }}>
                    <Button
                      variant="outlined"
                      size="small"
                      startIcon={isTesting ? <CircularProgress size={14} color="inherit" /> : <RefreshCw size={14} />}
                      disabled={isTesting}
                      onClick={() => handleTest(s.id)}
                      sx={{
                        borderColor: 'rgba(56, 189, 248, 0.3)',
                        color: '#38bdf8',
                        '&:hover': {
                          borderColor: '#38bdf8',
                          bgcolor: 'rgba(56, 189, 248, 0.1)',
                        },
                      }}
                    >
                      {isTesting ? 'Testing Protocol...' : 'Run Connection Probe'}
                    </Button>
                  </CardActions>
                </Card>
              </Grid>
            );
          })}
        </Grid>
      )}
    </Box>
  );
};
