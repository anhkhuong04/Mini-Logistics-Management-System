import { useQuery } from '@tanstack/react-query';
import { readOperations, readShippers } from './api';
import type { OperationsFilters } from './models';
export const operationsKey = (userId: string) => ['user', userId, 'operations'] as const;
export function useOperationsPage<T>(userId: string, resource: string, filters: OperationsFilters, enabled = true) {
  return useQuery({ queryKey: [...operationsKey(userId), resource, filters], queryFn: ({ signal }) => readOperations<T>(resource, filters, signal), enabled });
}
export function useOperationsShippers(userId: string) {
  return useQuery({ queryKey: [...operationsKey(userId), 'shippers'], queryFn: ({ signal }) => readShippers(signal) });
}
