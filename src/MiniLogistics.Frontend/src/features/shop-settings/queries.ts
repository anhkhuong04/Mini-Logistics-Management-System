import { readUtcDateRange } from '../../shared/lib/dateRange';
export function settingsFilters(search: URLSearchParams, maximum?: number) {
  for (const key of ['fromUtc', 'toUtc', 'action', 'limit'])
    if (search.getAll(key).length > 1) throw new Error('Duplicate query');
  const { fromUtc, toUtc } = readUtcDateRange(search);
  const action = search.get('action') || undefined;
  const rawLimit = search.get('limit');
  if (
    (action && action.length > 120) ||
    (rawLimit && (!/^\d+$/.test(rawLimit) || Number(rawLimit) < 1 || Number(rawLimit) > (maximum ?? 500)))
  )
    throw new Error('Invalid limit');
  return { fromUtc, toUtc, action, limit: rawLimit ? Number(rawLimit) : undefined };
}
