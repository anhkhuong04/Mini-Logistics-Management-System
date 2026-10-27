import { requestJson } from '../../shared/api/httpClient';
import type { statusLabels } from '../../shared/i18n/shipmentCopy';
export type PublicBanner = { id: string; title: string; imageUrl: string; linkUrl: string | null };
type Status = keyof typeof statusLabels.en;
export type PublicTracking = {
  trackingCode: string;
  accessLevel: 'Summary';
  currentStatus: Status;
  pickupProvince: string;
  deliveryProvince: string;
  createdAtUtc: string;
  lastUpdatedAtUtc: string;
  senderName: string | null;
  receiverName: string | null;
  serverTimeZoneId: string;
  timeline: { status: Status; messageCode: string; message: string; locale: string; changedAtUtc: string }[];
};
export class TrackingReadError extends Error {
  constructor(
    public status: number,
    public retryAfter: number | null,
  ) {
    super('Public tracking request failed');
  }
}
export async function readTracking(code: string, signal: AbortSignal): Promise<PublicTracking> {
  const response = await fetch(`/api/v1/public/tracking/${encodeURIComponent(code)}`, {
    signal,
    credentials: 'same-origin',
    cache: 'no-store',
    headers: { Accept: 'application/json, application/problem+json' },
  });
  if (!response.ok) {
    const retry = Number(response.headers.get('Retry-After'));
    throw new TrackingReadError(
      response.status,
      Number.isFinite(retry) && retry > 0 ? Math.ceil(retry) : null,
    );
  }
  if (!(response.headers.get('Content-Type') ?? '').includes('application/json'))
    throw new TrackingReadError(0, null);
  return response.json() as Promise<PublicTracking>;
}
export const readBanners = (signal: AbortSignal) =>
  requestJson<PublicBanner[]>('/api/v1/public/banners', { signal });
export function safeBannerLink(value: string | null): string | undefined {
  if (
    !value ||
    value !== value.trim() ||
    /[\s\\]/.test(value) ||
    Array.from(value).some((character) => character.charCodeAt(0) < 32 || character.charCodeAt(0) === 127)
  )
    return undefined;
  if (value.startsWith('/') && !value.startsWith('//')) return value;
  try {
    const url = new URL(value);
    return ['http:', 'https:'].includes(url.protocol) ? value : undefined;
  } catch {
    return undefined;
  }
}
