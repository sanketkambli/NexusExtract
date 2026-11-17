import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { apiClient } from './client';
import {
  CanonicalFieldDefinition,
  AnalyzeSchemaRequest,
  AnalyzeSchemaResponse,
  ApproveSchemaRequest,
  SchemaMappingSession,
} from '../types/api';

export const useCanonicalSchema = () => {
  return useQuery({
    queryKey: ['canonical-schema'],
    queryFn: async () => {
      const { data } = await apiClient.get<CanonicalFieldDefinition[]>('/schema-mapping/canonical-schema');
      return data;
    },
    staleTime: 1000 * 60 * 10, // 10 minutes cache
  });
};

export const useSchemaSessions = () => {
  return useQuery({
    queryKey: ['schema-sessions'],
    queryFn: async () => {
      const { data } = await apiClient.get<SchemaMappingSession[]>('/schema-mapping/sessions');
      return data;
    },
  });
};

export const useAnalyzeSchema = () => {
  return useMutation({
    mutationFn: async (req: AnalyzeSchemaRequest) => {
      const { data } = await apiClient.post<AnalyzeSchemaResponse>('/schema-mapping/analyze', req);
      return data;
    },
  });
};

export const useApproveSchema = () => {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async ({ sessionId, req }: { sessionId: string; req: ApproveSchemaRequest }) => {
      const { data } = await apiClient.post<SchemaMappingSession>(`/schema-mapping/${sessionId}/approve`, req);
      return data;
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['schema-sessions'] });
    },
  });
};
