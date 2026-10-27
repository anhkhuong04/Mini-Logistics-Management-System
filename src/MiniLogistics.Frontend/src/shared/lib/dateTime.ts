export function formatDateTime(utcIso: string, timeZoneId: string, locale: string) {
  if (!/(Z|[+-]\d{2}:\d{2})$/.test(utcIso)) throw new Error('Timestamp must include an offset');
  return new Intl.DateTimeFormat(locale, {
    dateStyle: 'medium',
    timeStyle: 'short',
    timeZone: timeZoneId,
  }).format(new Date(utcIso));
}
