export function timestamp(value?: string) {
  if (!value) return undefined;
  // Legacy Razor query timestamps without a suffix were explicitly interpreted as UTC.
  const normalized = /^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}(?:\.\d{1,7})?$/.test(value) ? `${value}Z` : value;
  if (
    !/^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}(?:\.\d{1,7})?(?:Z|[+-]\d{2}:\d{2})$/.test(normalized) ||
    Number.isNaN(Date.parse(normalized))
  )
    throw new Error('Invalid UTC timestamp');
  const day = normalized.slice(0, 10);
  if (new Date(`${day}T00:00:00Z`).toISOString().slice(0, 10) !== day) throw new Error('Invalid date');
  return normalized;
}
export function utcDateRange(from: string, to: string) {
  const fromUtc = from ? timestamp(`${from}T00:00:00Z`) : undefined;
  const toUtc = to ? timestamp(`${to}T23:59:59.9999999Z`) : undefined;
  if (fromUtc && toUtc && fromUtc > toUtc) throw new Error('Invalid date range');
  return { fromUtc, toUtc };
}

export function readUtcDateRange(search: URLSearchParams) {
  const get = (key: string) => {
    if (search.getAll(key).length > 1) throw new Error('Duplicate filter');
    return search.get(key)?.trim() || undefined;
  };
  const fromUtc = timestamp(get('fromUtc'));
  const toUtc = timestamp(get('toUtc'));
  if (fromUtc && toUtc && Date.parse(fromUtc) > Date.parse(toUtc)) throw new Error('Invalid date range');
  return { fromUtc, toUtc };
}
