import React from 'react';
import { Grid, Paper, Box, Typography } from '@mui/material';
import { Activity, CheckCircle2, HardDrive, Cpu } from 'lucide-react';
import { JobStatusResponse, DataSourceDto } from '../types/api';

interface MetricCardsProps {
  jobs: JobStatusResponse[];
  sources: DataSourceDto[];
}

export const MetricCards: React.FC<MetricCardsProps> = ({ jobs, sources }) => {
  const activeJobs = jobs.filter(
    (j) => j.status === 'Connecting' || j.status === 'Extracting' || j.status === 'StreamingToSink' || j.status === 'Queued'
  ).length;

  const completedJobs = jobs.filter((j) => j.status === 'Completed').length;

  const totalBytes = jobs.reduce((acc, j) => acc + (j.bytesExtracted || 0), 0);
  const totalMb = (totalBytes / (1024 * 1024)).toFixed(1);

  const healthySources = sources.filter((s) => s.healthStatus === 'Healthy').length;

  const cards = [
    {
      title: 'Active In-Flight Extracts',
      value: activeJobs,
      sub: activeJobs > 0 ? 'Streaming to destination sinks' : 'Idle / Queue Ready',
      icon: <Activity size={22} color="#38bdf8" />,
      color: '#38bdf8',
      bg: 'rgba(56, 189, 248, 0.08)',
    },
    {
      title: 'Successful Extractions',
      value: completedJobs,
      sub: 'Verified with SHA-256 Checksum',
      icon: <CheckCircle2 size={22} color="#10b981" />,
      color: '#10b981',
      bg: 'rgba(16, 185, 129, 0.08)',
    },
    {
      title: 'Volume Streamed (Session)',
      value: `${totalMb} MB`,
      sub: 'Zero RAM overhead streaming pipeline',
      icon: <HardDrive size={22} color="#a855f7" />,
      color: '#a855f7',
      bg: 'rgba(168, 85, 247, 0.08)',
    },
    {
      title: 'Configured Source Adapters',
      value: `${healthySources} / ${sources.length || 4}`,
      sub: 'Geneva, Sybase, SFTP, REST',
      icon: <Cpu size={22} color="#f59e0b" />,
      color: '#f59e0b',
      bg: 'rgba(245, 158, 11, 0.08)',
    },
  ];

  return (
    <Grid container spacing={2.5}>
      {cards.map((c, i) => (
        <Grid item xs={12} sm={6} md={3} key={i}>
          <Paper
            sx={{
              p: 2.5,
              borderRadius: 3,
              bgcolor: '#0f172a',
              border: '1px solid rgba(255,255,255,0.06)',
              transition: 'transform 0.2s, border-color 0.2s',
              '&:hover': {
                borderColor: 'rgba(56, 189, 248, 0.3)',
                transform: 'translateY(-2px)',
              },
            }}
          >
            <Box sx={{ display: 'flex', justifyContent: 'space-between', alignItems: 'flex-start' }}>
              <Box>
                <Typography variant="caption" sx={{ color: '#94a3b8', fontWeight: 500 }}>
                  {c.title}
                </Typography>
                <Typography variant="h5" sx={{ mt: 0.5, fontWeight: 700, color: '#f8fafc' }}>
                  {c.value}
                </Typography>
                <Typography variant="caption" sx={{ color: '#64748b', display: 'block', mt: 0.5 }}>
                  {c.sub}
                </Typography>
              </Box>
              <Box
                sx={{
                  p: 1.2,
                  borderRadius: 2,
                  bgcolor: c.bg,
                  display: 'flex',
                  alignItems: 'center',
                  justifyContent: 'center',
                }}
              >
                {c.icon}
              </Box>
            </Box>
          </Paper>
        </Grid>
      ))}
    </Grid>
  );
};
