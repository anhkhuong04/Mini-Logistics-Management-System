import type { components } from '../../shared/api/generated/browser-api';
import { requestJson } from '../../shared/api/httpClient';
export type IntegrationDashboard = components['schemas']['BrowserIntegrationDashboardResponse'];
export type IntegrationClient = components['schemas']['BrowserIntegrationClientResponse'];
export type ClientSecret = components['schemas']['PartnerApiClientSecretResponse'];
export type SecurityBody = {
  scopes: string[];
  allowedIpAddresses: string[];
  expiresAtUtc: string | null;
  name?: string;
};
export const scopes = ['Quote', 'CreateShipment', 'TrackShipment', 'CancelShipment', 'WebhookManage'];
export const integrationPath = (part: string) => `/api/v1/app/integrations/${part}`;
export const integrationWrite = <T>(
  part: string,
  method: 'POST' | 'PUT',
  body: unknown,
  signal: AbortSignal,
) => requestJson<T>(integrationPath(part), { method, body, signal });
