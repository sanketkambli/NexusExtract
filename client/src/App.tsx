import React, { useState } from 'react';
import { Box, Container, CssBaseline, ThemeProvider } from '@mui/material';
import { theme } from './theme';
import { Header } from './components/Header';
import { MetricCards } from './components/MetricCards';
import { DashboardPage } from './pages/DashboardPage';
import { TriggerExtractPage } from './pages/TriggerExtractPage';
import { DiagnosticsPage } from './pages/DiagnosticsPage';
import { SourcesPage } from './pages/SourcesPage';
import { SchemaMappingPage } from './pages/SchemaMappingPage';
import { useRecentJobs, useSources } from './api/jobsApi';

export const App: React.FC = () => {
  const [currentTab, setCurrentTab] = useState<'dashboard' | 'trigger' | 'diagnostics' | 'sources' | 'schema-mapping'>('dashboard');
  const [selectedJobIdForDiagnostics, setSelectedJobIdForDiagnostics] = useState<string | null>(null);

  const { data: jobs = [], isLoading: isJobsLoading } = useRecentJobs();
  const { data: sources = [] } = useSources();

  const handleSelectJobForDiagnostics = (jobId: string) => {
    setSelectedJobIdForDiagnostics(jobId);
    setCurrentTab('diagnostics');
  };

  return (
    <ThemeProvider theme={theme}>
      <CssBaseline />
      <Box sx={{ minHeight: '100vh', bgcolor: '#070b14', display: 'flex', flexDirection: 'column' }}>
        <Header currentTab={currentTab} onTabChange={(tab: any) => setCurrentTab(tab)} />

        <Container maxWidth="xl" sx={{ py: 3, flex: 1 }}>
          {currentTab === 'dashboard' && (
            <>
              <Box sx={{ mb: 3 }}>
                <MetricCards jobs={jobs} sources={sources} />
              </Box>
              <DashboardPage
                jobs={jobs}
                isLoading={isJobsLoading}
                onSelectJobForDiagnostics={handleSelectJobForDiagnostics}
                onOpenTriggerModal={() => setCurrentTab('trigger')}
              />
            </>
          )}

          {currentTab === 'trigger' && (
            <TriggerExtractPage onSuccessRedirect={() => setCurrentTab('dashboard')} />
          )}

          {currentTab === 'diagnostics' && (
            <DiagnosticsPage initialJobId={selectedJobIdForDiagnostics} />
          )}

          {currentTab === 'sources' && <SourcesPage />}

          {currentTab === 'schema-mapping' && <SchemaMappingPage />}
        </Container>
      </Box>
    </ThemeProvider>
  );
};

export default App;
