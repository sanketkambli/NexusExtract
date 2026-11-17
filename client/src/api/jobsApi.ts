import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { apiClient } from './client';
import {
  JobStatusResponse,
  JobDiagnosticResponse,
  DataSourceDto,
  TriggerExtractionRequest,
  ConnectionHealthResult,
} from '../types/api';

export const useRecentJobs = (limit = 50) => {
  return useQuery({
    queryKey: ['jobs', limit],
    queryFn: async () => {
      const { data } = await apiClient.get<JobStatusResponse[]>(`/extractions/jobs?limit=${limit}`);
      return data;
    },
    refetchInterval: 2500, // Real-time polling for in-flight streams
  });
};

export const useJobStatus = (jobId: string | null) => {
  return useQuery({
    queryKey: ['job', jobId],
    queryFn: async () => {
      if (!jobId) return null;
      const { data } = await apiClient.get<JobStatusResponse>(`/extractions/jobs/${jobId}`);
      return data;
    },
    enabled: !!jobId,
    refetchInterval: (query) => {
      const status = query.state.data?.status;
      return status === 'Completed' || status === 'Failed' || status === 'Cancelled' || status === 'DeadLettered' ? false : 2000;
    },
  });
};

export const useReplayJob = () => {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async (jobId: string) => {
      const { data } = await apiClient.post<JobStatusResponse>(`/extractions/jobs/${jobId}/replay`);
      return data;
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['jobs'] });
      queryClient.invalidateQueries({ queryKey: ['job'] });
      queryClient.invalidateQueries({ queryKey: ['job-diagnostics'] });
    },
  });
};

export const useJobDiagnostics = (jobId: string | null) => {
  return useQuery({
    queryKey: ['job-diagnostics', jobId],
    queryFn: async () => {
      if (!jobId) return null;
      const { data } = await apiClient.get<JobDiagnosticResponse>(`/extractions/jobs/${jobId}/diagnostics`);
      return data;
    },
    enabled: !!jobId,
  });
};

export const useTriggerExtraction = () => {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async (req: TriggerExtractionRequest) => {
      const { data } = await apiClient.post<JobStatusResponse>('/extractions/jobs', req);
      return data;
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['jobs'] });
    },
  });
};

export const useCancelJob = () => {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async (jobId: string) => {
      const { data } = await apiClient.post(`/extractions/jobs/${jobId}/cancel`);
      return data;
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['jobs'] });
    },
  });
};

export const useSources = () => {
  return useQuery({
    queryKey: ['sources'],
    queryFn: async () => {
      const { data } = await apiClient.get<DataSourceDto[]>('/sources');
      return data;
    },
  });
};

export const useTestConnection = () => {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async (sourceId: string) => {
      const { data } = await apiClient.post<ConnectionHealthResult>(`/sources/${sourceId}/test-connection`);
      return data;
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['sources'] });
    },
  });
};
