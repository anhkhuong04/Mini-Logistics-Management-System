import { useQuery } from '@tanstack/react-query';
import { readWorkspace, readProfile, readAreas, readSummary } from './api';
import type { ShipperFilters } from './models';
export const shipperKey = (userId: string) => ['user', userId, 'shipper'] as const;
export function useShipperQueries(userId: string, filters: ShipperFilters, valid: boolean) {
  const { businessDate, ...workspaceFilters } = filters;
  const workspace = useQuery({ queryKey: [...shipperKey(userId), 'workspace', workspaceFilters], queryFn: ({ signal }) => readWorkspace(workspaceFilters, signal), enabled: valid });
  const profile = useQuery({ queryKey: [...shipperKey(userId), 'profile'], queryFn: ({ signal }) => readProfile(signal) });
  const areas = useQuery({ queryKey: [...shipperKey(userId), 'areas'], queryFn: ({ signal }) => readAreas(signal) });
  const summary = useQuery({ queryKey: [...shipperKey(userId), 'summary', businessDate ?? 'today'], queryFn: ({ signal }) => readSummary(businessDate, signal), enabled: valid });
  return { workspace, profile, areas, summary };
}
