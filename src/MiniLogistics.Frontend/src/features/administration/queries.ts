import { useQuery } from '@tanstack/react-query';
import { useSearchParams } from 'react-router';
import { useSession } from '../../shared/auth/SessionContext';
import { adminKey, readAdmin } from './api';
export function useAdminRead<T>(part: string, filters = true) {
  const { session } = useSession();
  const [search] = useSearchParams();
  return useQuery({
    queryKey: [...adminKey(session!.userId), part, filters ? search.toString() : ''],
    queryFn: ({ signal }) => readAdmin<T>(`${part}${filters ? `?${search}` : ''}`, signal),
    retry: false,
  });
}
