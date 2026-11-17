import axios from 'axios';

// Base API URL: uses Vite proxy or fallback
export const apiClient = axios.create({
  baseURL: import.meta.env.VITE_API_URL || 'http://localhost:5000/api/v1',
  headers: {
    'Content-Type': 'application/json',
  },
});

// Request interceptor: automatically generates or attaches correlation ID
apiClient.interceptors.request.use((config) => {
  if (!config.headers['X-Correlation-ID']) {
    const correlationId = 'ui-' + Math.random().toString(36).substring(2, 11) + '-' + Date.now();
    config.headers['X-Correlation-ID'] = correlationId;
  }
  return config;
});
