import { QueryClient } from '@tanstack/react-query';
import { ApiError } from './httpClient';
export function createQueryClient() {
  return new QueryClient({ defaultOptions: {
    queries: { retry: (count, error) => count < 2 && (error instanceof TypeError || error instanceof ApiError && error.status >= 500) },
    mutations: { retry: false },
  } });
}
