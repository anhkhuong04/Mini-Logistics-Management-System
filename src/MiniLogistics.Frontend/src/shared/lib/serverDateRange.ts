import dayjs from 'dayjs';
import utc from 'dayjs/plugin/utc';
import timezone from 'dayjs/plugin/timezone';
dayjs.extend(utc);
dayjs.extend(timezone);

export function serverDateLabel(instant: string | undefined, timeZone: string) {
  return instant ? dayjs.utc(instant).tz(timeZone).format('YYYY-MM-DD') : undefined;
}
export function serverDateRange(from: string, to: string, timeZone: string) {
  for (const date of [from, to])
    if (date && (!/^\d{4}-\d{2}-\d{2}$/.test(date) || dayjs.utc(date).format('YYYY-MM-DD') !== date))
      throw new Error('Invalid date');
  if (from && to && from > to) throw new Error('Invalid date range');
  return {
    fromUtc: from ? dayjs.tz(`${from}T00:00:00`, timeZone).utc().format('YYYY-MM-DDTHH:mm:ss[Z]') : undefined,
    // Preserve .NET's seven-digit end-of-day precision without passing ticks through JS milliseconds.
    toUtc: to
      ? `${dayjs.tz(`${to}T23:59:59`, timeZone).utc().format('YYYY-MM-DDTHH:mm:ss')}.9999999Z`
      : undefined,
  };
}
