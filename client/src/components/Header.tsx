import React from 'react';
import {
  AppBar,
  Toolbar,
  Typography,
  Box,
  Button,
  Chip,
  IconButton,
} from '@mui/material';
import {
  Layers,
  Activity,
  PlayCircle,
  HelpCircle,
  Database,
  Terminal,
} from 'lucide-react';

interface HeaderProps {
  currentTab: string;
  onTabChange: (tab: string) => void;
}

export const Header: React.FC<HeaderProps> = ({ currentTab, onTabChange }) => {
  return (
    <AppBar position="sticky" sx={{ bgcolor: '#0b1120', borderBottom: '1px solid rgba(255,255,255,0.08)' }} elevation={0}>
      <Toolbar sx={{ justifyContent: 'space-between', px: 3, minHeight: 64 }}>
        {/* Brand */}
        <Box sx={{ display: 'flex', alignItems: 'center', gap: 2 }}>
          <Box
            sx={{
              display: 'flex',
              alignItems: 'center',
              justifyContent: 'center',
              width: 38,
              height: 38,
              borderRadius: 2,
              background: 'linear-gradient(135deg, #0284c7 0%, #38bdf8 100%)',
              color: '#fff',
              boxShadow: '0 0 15px rgba(56, 189, 248, 0.4)',
            }}
          >
            <Layers size={22} />
          </Box>
          <Box>
            <Typography variant="h6" sx={{ fontWeight: 700, color: '#f8fafc', lineHeight: 1.2 }}>
              NexusExtract <span style={{ color: '#38bdf8', fontWeight: 400 }}>Gateway</span>
            </Typography>
            <Typography variant="caption" sx={{ color: '#64748b', fontSize: '0.7rem' }}>
              Streaming Ingestion & Diagnostics Engine
            </Typography>
          </Box>
          <Chip
            label="NET 10 • STREAMING"
            size="small"
            sx={{
              ml: 2,
              bgcolor: 'rgba(56, 189, 248, 0.1)',
              color: '#38bdf8',
              fontWeight: 600,
              fontSize: '0.68rem',
              border: '1px solid rgba(56, 189, 248, 0.25)',
            }}
          />
        </Box>

        {/* Navigation Tabs */}
        <Box sx={{ display: 'flex', alignItems: 'center', gap: 1 }}>
          <Button
            startIcon={<Activity size={17} />}
            variant={currentTab === 'dashboard' ? 'contained' : 'text'}
            onClick={() => onTabChange('dashboard')}
            sx={{
              bgcolor: currentTab === 'dashboard' ? 'rgba(56, 189, 248, 0.15)' : 'transparent',
              color: currentTab === 'dashboard' ? '#38bdf8' : '#94a3b8',
              border: currentTab === 'dashboard' ? '1px solid rgba(56, 189, 248, 0.3)' : '1px solid transparent',
              '&:hover': { bgcolor: 'rgba(56, 189, 248, 0.1)' },
            }}
          >
            Live Monitor
          </Button>

          <Button
            startIcon={<PlayCircle size={17} />}
            variant={currentTab === 'trigger' ? 'contained' : 'text'}
            onClick={() => onTabChange('trigger')}
            sx={{
              bgcolor: currentTab === 'trigger' ? 'rgba(56, 189, 248, 0.15)' : 'transparent',
              color: currentTab === 'trigger' ? '#38bdf8' : '#94a3b8',
              border: currentTab === 'trigger' ? '1px solid rgba(56, 189, 248, 0.3)' : '1px solid transparent',
              '&:hover': { bgcolor: 'rgba(56, 189, 248, 0.1)' },
            }}
          >
            Trigger Extract
          </Button>

          <Button
            startIcon={<HelpCircle size={17} />}
            variant={currentTab === 'diagnostics' ? 'contained' : 'text'}
            onClick={() => onTabChange('diagnostics')}
            sx={{
              bgcolor: currentTab === 'diagnostics' ? 'rgba(56, 189, 248, 0.15)' : 'transparent',
              color: currentTab === 'diagnostics' ? '#38bdf8' : '#94a3b8',
              border: currentTab === 'diagnostics' ? '1px solid rgba(56, 189, 248, 0.3)' : '1px solid transparent',
              '&:hover': { bgcolor: 'rgba(56, 189, 248, 0.1)' },
            }}
          >
            Diagnostics Console
          </Button>

          <Button
            startIcon={<Database size={17} />}
            variant={currentTab === 'sources' ? 'contained' : 'text'}
            onClick={() => onTabChange('sources')}
            sx={{
              bgcolor: currentTab === 'sources' ? 'rgba(56, 189, 248, 0.15)' : 'transparent',
              color: currentTab === 'sources' ? '#38bdf8' : '#94a3b8',
              border: currentTab === 'sources' ? '1px solid rgba(56, 189, 248, 0.3)' : '1px solid transparent',
              '&:hover': { bgcolor: 'rgba(56, 189, 248, 0.1)' },
            }}
          >
            Source Adapters
          </Button>
        </Box>

        {/* Status Indicator */}
        <Box sx={{ display: 'flex', alignItems: 'center', gap: 1.5 }}>
          <Box
            sx={{
              display: 'flex',
              alignItems: 'center',
              gap: 1,
              px: 1.5,
              py: 0.5,
              borderRadius: 20,
              bgcolor: 'rgba(16, 185, 129, 0.1)',
              border: '1px solid rgba(16, 185, 129, 0.25)',
            }}
          >
            <Box
              sx={{
                width: 8,
                height: 8,
                borderRadius: '50%',
                bgcolor: '#10b981',
                boxShadow: '0 0 8px #10b981',
              }}
            />
            <Typography variant="caption" sx={{ color: '#10b981', fontWeight: 600 }}>
              Engine Live
            </Typography>
          </Box>
        </Box>
      </Toolbar>
    </AppBar>
  );
};
