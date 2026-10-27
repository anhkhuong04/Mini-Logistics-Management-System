import { useQuery } from '@tanstack/react-query';
import type { components } from './generated/browser-api';
import { requestJson } from './httpClient';
export type ProvinceData = components['schemas']['BrowserProvinceData'];
export function useProvinces() {
  return useQuery({
    queryKey: ['public', 'provinces'],
    queryFn: ({ signal }) => requestJson<ProvinceData>('/api/v1/public/reference-data/provinces', { signal }),
  });
}
